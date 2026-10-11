import { chromium, expect, test } from "@playwright/test";
import { execFileSync, spawn, spawnSync, type ChildProcess } from "node:child_process";
import { existsSync, mkdirSync, mkdtempSync, readdirSync, writeFileSync } from "node:fs";
import { createServer } from "node:net";
import { homedir, tmpdir } from "node:os";
import { join } from "node:path";
import { messages } from "../src/i18n/messages";

// M5 installer test (Windows only, on demand): SST_INSTALLER=<path to SmartSchoolTimetable-Setup-x.y.z.exe>.
// Everything happens in a temporary folder: the program is installed for the current user only (/CURRENTUSER, no administrator), its data lives in
// SST_DATA_DIR (never the real %LOCALAPPDATA%\SmartSchoolTimetable), and the program's window is reached through WebView2's debugging port.
const setup = process.env.SST_INSTALLER ?? "";
test.skip(setup === "" || process.platform !== "win32", "On demand: set SST_INSTALLER to the Setup.exe.");

const root = mkdtempSync(join(tmpdir(), "sst-install-"));
const appDir = join(root, "app");
const dataDir = join(root, "data");
const exe = join(appDir, "SmartSchoolTimetable.exe");
const startMenu = join(process.env.APPDATA ?? join(homedir(), "AppData", "Roaming"), "Microsoft", "Windows", "Start Menu", "Programs", "الجدول الذكي");

function install(extra: string[] = []) {
  mkdirSync(root, { recursive: true });
  return spawnSync(setup, ["/CURRENTUSER", "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", `/DIR=${appDir}`, `/LOG=${join(root, "setup.log")}`, "/TASKS=", ...extra], { timeout: 180_000 });
}

const processes = (name: string) => {
  const output = execFileSync("powershell", ["-NoProfile", "-Command", `@(Get-Process -Name '${name}' -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '${appDir.replaceAll("'", "''")}*' }).Count`]).toString().trim();
  return Number(output);
};

async function waitFor(check: () => boolean | Promise<boolean>, seconds: number, what: string) {
  for (let waited = 0; waited < seconds; waited++) {
    if (await check()) return;
    await new Promise((done) => setTimeout(done, 1000));
  }
  throw new Error(`Timed out waiting for ${what}`);
}

function launch(extraEnv: Record<string, string> = {}, args: string[] = []): ChildProcess {
  return spawn(exe, args, { env: { ...process.env, SST_DATA_DIR: dataDir, WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS: "--remote-debugging-port=9334", ...extraEnv }, stdio: "ignore", detached: false });
}

const debugEndpoint = "http://127.0.0.1:9334";
const cdpReady = async () => { try { return (await fetch(`${debugEndpoint}/json/version`)).ok; } catch { return false; } };
const stopProgram = () => spawnSync(exe, ["--stop"], { env: { ...process.env, SST_DATA_DIR: dataDir }, timeout: 30_000 });

test.describe.configure({ mode: "serial" });

test("(M5) install: files, no data, shortcut, first-run screen in the WebView2 window", async () => {
  test.setTimeout(300_000);
  const result = install();
  expect(result.status, `Setup exit code (log: ${join(root, "setup.log")})`).toBe(0);
  for (const file of ["SmartSchoolTimetable.exe", "SmartSchoolTimetable.Api.exe", "LICENSE.txt", "wwwroot/index.html", "google-ortools-native.dll", "unins000.exe"]) expect(existsSync(join(appDir, file)), file).toBe(true);
  const all = readdirSync(appDir, { recursive: true }).map(String);
  expect(all.filter((name) => /\.(db|db-wal|db-shm|log|pdb)$/i.test(name)), "no database, log or symbol file is installed").toEqual([]);
  expect(existsSync(join(startMenu, "الجدول الذكي.lnk")), "Start-menu shortcut").toBe(true);

  launch();
  await waitFor(cdpReady, 90, "the WebView2 window");
  const browser = await chromium.connectOverCDP(debugEndpoint);
  const page = browser.contexts()[0].pages()[0];
  await expect(page.getByRole("heading", { name: messages.app.setupTitle })).toBeVisible({ timeout: 60_000 }); // an empty database: the first-run account screen
  expect(page.url()).toMatch(/^http:\/\/127\.0\.0\.1:\d+\//);
  await browser.close();

  // The server runs hidden: one launcher, one server, the server has no window of its own.
  expect(processes("SmartSchoolTimetable")).toBe(1);
  expect(processes("SmartSchoolTimetable.Api")).toBe(1);
  const windowHandle = execFileSync("powershell", ["-NoProfile", "-Command", `(Get-Process -Name 'SmartSchoolTimetable.Api' | Select-Object -First 1).MainWindowHandle`]).toString().trim();
  expect(windowHandle).toBe("0");
});

test("(M5) a second start only shows the running program; «--backup-now» writes a copy; «--stop» stops it and keeps the data", async () => {
  test.setTimeout(240_000);
  const started = Date.now();
  const second = spawnSync(exe, [], { env: { ...process.env, SST_DATA_DIR: dataDir }, timeout: 30_000 });
  expect(second.status).toBe(0);
  expect(Date.now() - started, "the second copy ends at once").toBeLessThan(15_000);
  expect(processes("SmartSchoolTimetable")).toBe(1);
  expect(processes("SmartSchoolTimetable.Api")).toBe(1);

  // The tray's «نسخة احتياطية الآن» through the running copy: a verified copy appears in the backup folder.
  spawnSync(exe, ["--backup-now"], { env: { ...process.env, SST_DATA_DIR: dataDir }, timeout: 30_000 });
  const backups = join(dataDir, "owner-backups");
  await waitFor(() => existsSync(backups) && readdirSync(backups).some((name) => /^timetable-backup-.*\.db$/.test(name)), 60, "the tray backup");

  // «إيقاف»: both processes end; the database and the backup stay.
  stopProgram();
  await waitFor(() => processes("SmartSchoolTimetable") === 0 && processes("SmartSchoolTimetable.Api") === 0, 60, "the program to stop");
  expect(existsSync(join(dataDir, "timetable.db"))).toBe(true);
  expect(readdirSync(backups).some((name) => /^timetable-backup-/.test(name))).toBe(true);
});

test("(M5) port 5080 busy: the program uses another free port and its protections still hold", async () => {
  test.setTimeout(240_000);
  const blocker = createServer();
  await new Promise<void>((done, fail) => { blocker.once("error", fail); blocker.listen(5080, "127.0.0.1", () => done()); });
  try {
    launch();
    await waitFor(cdpReady, 90, "the WebView2 window");
    const browser = await chromium.connectOverCDP(debugEndpoint);
    const page = browser.contexts()[0].pages()[0];
    await expect(page.getByRole("heading", { name: messages.app.setupTitle })).toBeVisible({ timeout: 60_000 });
    const origin = new URL(page.url()).origin;
    expect(new URL(origin).port).not.toBe("5080");
    await browser.close();
    // Host, Origin and the launch token are enforced on the new port as on 5080.
    expect((await fetch(`${origin}/api/v1/bootstrap`)).status).toBe(200);
    expect((await fetch(`${origin}/api/v1/auth/logout`, { method: "POST" })).status).toBe(403); // no Origin, no token
    expect((await fetch(`${origin}/api/v1/bootstrap`, { headers: { Origin: "https://evil.example" } })).status).toBe(403);
  } finally {
    stopProgram();
    await waitFor(() => processes("SmartSchoolTimetable") === 0 && processes("SmartSchoolTimetable.Api") === 0, 60, "the program to stop");
    blocker.close();
  }
});

test("(M5) without the WebView2 runtime the program falls back to the default browser and still serves", async () => {
  test.setTimeout(240_000);
  launch({ SST_BROWSER_ONLY: "1", SST_NO_BROWSER: "1", WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS: "" });
  await waitFor(() => processes("SmartSchoolTimetable.Api") === 1, 90, "the server");
  // The server's port (5080 or a free one) is read from the operating system, then the server is asked for its first-run state.
  let origin = "";
  await waitFor(async () => {
    const port = execFileSync("powershell", ["-NoProfile", "-Command", "(Get-NetTCPConnection -State Listen -OwningProcess (Get-Process -Name 'SmartSchoolTimetable.Api' | Select-Object -First 1).Id -ErrorAction SilentlyContinue | Select-Object -First 1).LocalPort"]).toString().trim();
    origin = port ? `http://127.0.0.1:${port}` : "";
    try { return origin !== "" && (await fetch(`${origin}/api/v1/bootstrap`)).ok; } catch { return false; }
  }, 90, "the server to answer");
  expect(await (await fetch(`${origin}/api/v1/bootstrap`)).text()).toContain("launchToken");
  // No window engine was started by the launcher: it has no msedgewebview2 child process.
  const children = execFileSync("powershell", ["-NoProfile", "-Command", "$launcher = (Get-Process -Name 'SmartSchoolTimetable' | Select-Object -First 1).Id; @(Get-CimInstance Win32_Process | Where-Object { $_.ParentProcessId -eq $launcher -and $_.Name -like 'msedgewebview2*' }).Count"]).toString().trim();
  expect(Number(children)).toBe(0);
  stopProgram();
  await waitFor(() => processes("SmartSchoolTimetable") === 0 && processes("SmartSchoolTimetable.Api") === 0, 60, "the program to stop");
});

test("(M5) upgrade in place keeps the data; a running program stops the installer; uninstall removes the program and never the data", async () => {
  test.setTimeout(480_000);
  const before = readdirSync(dataDir, { recursive: true }).map(String).sort();
  expect(before.length).toBeGreaterThan(0);

  // Upgrade over the existing installation (the same version stands in for a newer one).
  expect(install().status, "upgrade").toBe(0);
  expect(existsSync(exe)).toBe(true);

  // A running program: the installer does not replace files under it.
  launch();
  await waitFor(cdpReady, 90, "the WebView2 window");
  const refused = install(["/NOCANCEL"]);
  expect(refused.status, "Setup must not succeed while the program runs").not.toBe(0);
  stopProgram();
  await waitFor(() => processes("SmartSchoolTimetable") === 0 && processes("SmartSchoolTimetable.Api") === 0, 60, "the program to stop");

  // Uninstall.
  const uninstall = spawnSync(join(appDir, "unins000.exe"), ["/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART"], { timeout: 180_000 });
  expect(uninstall.status).toBe(0);
  await waitFor(() => !existsSync(exe), 60, "the program files to be removed");
  expect(existsSync(join(startMenu, "الجدول الذكي.lnk"))).toBe(false);
  // The school's data is untouched.
  expect(readdirSync(dataDir, { recursive: true }).map(String).sort()).toEqual(expect.arrayContaining(before.filter((name) => !/WebView2/i.test(name))));
  expect(existsSync(join(dataDir, "timetable.db"))).toBe(true);
  writeFileSync(join(root, "done.txt"), "ok");
});
