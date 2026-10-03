import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import { spawn, type ChildProcess } from "node:child_process";
import { once } from "node:events";
import { mkdtemp, rm } from "node:fs/promises";
import { createServer } from "node:net";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { messages } from "../src/i18n/messages";

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const apiDirectory = join(repositoryRoot, "src", "SmartSchoolTimetable.Api");
const apiAssembly = join(apiDirectory, "bin", "Release", "net9.0", "SmartSchoolTimetable.Api.dll");
const dotnetHost = process.env.DOTNET_HOST_PATH ??
  join(process.env.ProgramFiles ?? "C:\\Program Files", "dotnet", "dotnet.exe");

let apiProcess: ChildProcess;
let databaseDirectory: string;
let baseUrl: string;
let output = "";
let errors = "";

async function freePort(): Promise<number> {
  const server = createServer();
  server.listen(0, "127.0.0.1");
  await once(server, "listening");
  const address = server.address();
  if (!address || typeof address === "string") throw new Error("Could not allocate a local test port.");
  const port = address.port;
  await new Promise<void>((resolveClose, reject) =>
    server.close((error) => error ? reject(error) : resolveClose()),
  );
  return port;
}

async function waitForApi(page: Page): Promise<void> {
  const startedAt = Date.now();
  while (Date.now() - startedAt < 30_000) {
    try {
      const response = await page.request.get(`${baseUrl}/api/v1/bootstrap`, { timeout: 1_000 });
      if (response.ok()) {
        const indexResponse = await page.request.get(baseUrl);
        const html = await indexResponse.text();
        const scriptPath = html.match(/src="([^"]+\.js)"/)?.[1];
        if (!scriptPath) throw new Error("The API index does not reference a built JavaScript asset.");
        const scriptResponse = await page.request.get(new URL(scriptPath, baseUrl).toString());
        if (!scriptResponse.ok()) {
          throw new Error(`The JavaScript asset returned HTTP ${scriptResponse.status()}.`);
        }
        return;
      }
    } catch {
      await new Promise((resolveWait) => setTimeout(resolveWait, 150));
    }
    if (apiProcess.exitCode !== null) throw new Error(`API exited early: ${output}\n${errors}`);
  }
  throw new Error(`API did not start: ${output}\n${errors}`);
}

test.beforeAll(async ({ browser }) => {
  const port = await freePort();
  baseUrl = `http://127.0.0.1:${port}`;
  databaseDirectory = await mkdtemp(join(tmpdir(), "smart-school-e2e-"));
  apiProcess = spawn(dotnetHost, [apiAssembly], {
    cwd: dirname(apiAssembly),
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: "Production",
      Database__Path: join(databaseDirectory, "e2e.db"),
      LocalHost__Port: String(port),
    },
    stdio: ["ignore", "pipe", "pipe"],
  });
  apiProcess.stdout?.on("data", (chunk: Buffer) => { output += chunk.toString(); });
  apiProcess.stderr?.on("data", (chunk: Buffer) => { errors += chunk.toString(); });
  const page = await browser.newPage({ baseURL: baseUrl });
  try {
    await waitForApi(page);
  } finally {
    await page.close();
  }
});

test.afterAll(async () => {
  if (apiProcess?.exitCode === null && apiProcess?.pid) {
    apiProcess.kill();
    await Promise.race([once(apiProcess, "exit"), new Promise((resolveWait) => setTimeout(resolveWait, 5_000))]);
  }
  if (databaseDirectory) await rm(databaseDirectory, { recursive: true, force: true });
});


// Both tests share one real API process and database and run in file order (fullyParallel: false).
const initialPassword = "Owner-88"; // exactly the 8-character minimum
const recoveredPassword = "A-New-Strong-Passphrase-802";
const recoveryCodePattern = /^[A-F0-9]{8}(-[A-F0-9]{8}){3}$/;

async function expectArabicAlert(page: Page, expected: string): Promise<void> {
  const alert = page.getByRole("alert");
  await expect(alert).toContainText(expected);
  expect(await alert.innerText()).not.toMatch(/[A-Za-z]/);
}

async function acknowledgeShownCode(page: Page): Promise<string> {
  await expect(page.getByRole("heading", { name: messages.app.codeTitle })).toBeVisible();
  const code = (await page.getByRole("status", { name: messages.app.recoveryCode }).textContent()) ?? "";
  expect(code).toMatch(recoveryCodePattern);
  await expect(page.getByRole("button", { name: messages.app.continue })).toBeDisabled();
  await page.getByLabel(messages.app.confirmCodeSaved).check();
  await page.getByRole("button", { name: messages.app.continue }).click();
  return code;
}

test("setup, blocked reload until a new code is confirmed, logout, login, and password recovery", async ({ page }) => {
  await page.goto(baseUrl);
  await expect(page.getByRole("heading", { name: messages.app.setupTitle })).toBeVisible();
  await page.getByLabel(messages.app.username).fill("owner");
  await page.getByLabel(messages.app.password, { exact: true }).fill(initialPassword);
  await page.getByLabel(messages.app.confirmPassword).fill(initialPassword);
  await page.getByRole("button", { name: messages.app.createAccount }).click();

  await expect(page.getByRole("heading", { name: messages.app.codeTitle })).toBeVisible();
  const originalRecoveryCode = await page.getByRole("status", { name: messages.app.recoveryCode }).textContent();
  expect(originalRecoveryCode).toMatch(recoveryCodePattern);
  await expect(page.getByRole("button", { name: messages.app.continue })).toBeDisabled();

  // Reload before acknowledging: the signed-in owner is blocked, not sent to Home or any app route.
  await page.reload();
  await expect(page.getByRole("heading", { name: messages.app.recoveryPendingTitle })).toBeVisible();
  await expect(page.getByRole("heading", { name: messages.app.home })).toHaveCount(0);
  await page.goto(`${baseUrl}/settings`);
  await expect(page.getByRole("heading", { name: messages.app.recoveryPendingTitle })).toBeVisible();
  await expect(page.getByRole("heading", { name: messages.app.settings })).toHaveCount(0);

  await page.getByLabel(messages.app.currentPassword).fill("Wrong-Password-Not-Valid");
  await page.getByRole("button", { name: messages.app.generateCode }).click();
  await expectArabicAlert(page, messages.errors.CURRENT_PASSWORD_INCORRECT);

  await page.getByLabel(messages.app.currentPassword).fill(initialPassword);
  await page.getByRole("button", { name: messages.app.generateCode }).click();
  const recoveryCode = await acknowledgeShownCode(page);
  expect(recoveryCode).not.toBe(originalRecoveryCode);
  await expect(page.getByRole("heading", { name: messages.app.settings })).toBeVisible();

  await page.getByRole("button", { name: messages.app.logout }).click();
  await expect(page.getByRole("heading", { name: messages.app.loginTitle })).toBeVisible();
  await page.getByLabel(messages.app.username).fill("owner");
  await page.getByLabel(messages.app.password, { exact: true }).fill("Wrong-Password-Not-Valid");
  await page.getByRole("button", { name: messages.app.loginAction }).click();
  await expectArabicAlert(page, messages.errors.INVALID_CREDENTIALS);

  await page.getByLabel(messages.app.password, { exact: true }).fill(initialPassword);
  await page.getByRole("button", { name: messages.app.loginAction }).click();
  await expect(page.getByRole("heading", { name: messages.app.home })).toBeVisible();
  await page.getByRole("button", { name: messages.app.logout }).click();

  await page.getByRole("button", { name: messages.app.recoveryLink }).click();
  await page.getByLabel(messages.app.recoveryCode).fill(recoveryCode);
  await page.getByLabel(messages.app.newPassword).fill(recoveredPassword);
  await page.getByLabel(messages.app.confirmPassword).fill(recoveredPassword);
  await page.getByRole("button", { name: messages.app.resetPassword }).click();
  await acknowledgeShownCode(page);
  await expect(page.getByRole("heading", { name: messages.app.home })).toBeVisible();

  // After a completed recovery, logging out returns to the login screen rather than the recovery form.
  await page.getByRole("button", { name: messages.app.logout }).click();
  await expect(page.getByRole("heading", { name: messages.app.loginTitle })).toBeVisible();
});

test("real validation and not-found responses, mocked 500, and a stopped server stay Arabic", async ({ browser }) => {
  const context: BrowserContext = await browser.newContext({ baseURL: baseUrl });
  const page = await context.newPage();

  // Real 422 from the server: the login form sends empty fields and the API returns field codes.
  await page.goto(baseUrl);
  await expect(page.getByRole("heading", { name: messages.app.loginTitle })).toBeVisible();
  const validationResponse = page.waitForResponse((response) => response.url().endsWith("/api/v1/auth/login"));
  await page.getByRole("button", { name: messages.app.loginAction }).click();
  expect((await validationResponse).status()).toBe(422);
  await expectArabicAlert(page, `${messages.fields.Username}: ${messages.errors.REQUIRED}`);

  // Real 404 from the API contract, and the real not-found page for an unknown app route.
  const realNotFound = await page.request.get(`${baseUrl}/api/v1/no-such-browser-route`);
  expect(realNotFound.status()).toBe(404);
  expect((await realNotFound.json()).code).toBe("NOT_FOUND");

  await page.getByLabel(messages.app.username).fill("owner");
  await page.getByLabel(messages.app.password, { exact: true }).fill(recoveredPassword);
  await page.getByRole("button", { name: messages.app.loginAction }).click();
  await expect(page.getByRole("heading", { name: messages.app.home })).toBeVisible();
  await page.goto(`${baseUrl}/no-such-page`);
  await expectArabicAlert(page, messages.app.notFoundPage);
  await expect(page.getByRole("alert")).toHaveCount(1);
  await page.getByRole("button", { name: messages.app.logout }).click();
  await expect(page.getByRole("heading", { name: messages.app.loginTitle })).toBeVisible();

  // An unexpected 500 cannot be produced on demand by the real server, so this path stays mocked.
  await page.route("**/api/v1/bootstrap", async (route) => {
    await route.fulfill({
      status: 500,
      contentType: "application/json",
      body: JSON.stringify({ code: "INTERNAL_ERROR", correlationId: "test-500", errors: [] }),
    });
  });
  await page.reload();
  await expectArabicAlert(page, messages.errors.INTERNAL_ERROR);
  await page.unroute("**/api/v1/bootstrap");

  // Stopped server: load the real login screen, stop the API process, then submit.
  await page.goto(baseUrl);
  await expect(page.getByRole("heading", { name: messages.app.loginTitle })).toBeVisible();
  apiProcess.kill();
  await once(apiProcess, "exit");
  await page.getByLabel(messages.app.username).fill("owner");
  await page.getByLabel(messages.app.password, { exact: true }).fill(recoveredPassword);
  await page.getByRole("button", { name: messages.app.loginAction }).click();
  await expectArabicAlert(page, messages.errors.NETWORK_ERROR);

  await context.close();
});
