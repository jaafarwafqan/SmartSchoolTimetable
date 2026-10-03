import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import { spawn, type ChildProcess } from "node:child_process";
import { once } from "node:events";
import { mkdtemp, rm } from "node:fs/promises";
import { createServer } from "node:net";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

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
  databaseDirectory = await mkdtemp(join(tmpdir(), "smart-school-phase-1-2-"));
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

test("setup, recovery confirmation, automatic entry, logout, login, and password recovery", async ({ page }) => {
  await page.goto(baseUrl);
  await expect(page.getByRole("heading", { name: "إعداد حساب المالك" })).toBeVisible();
  await page.getByLabel("اسم المستخدم").fill("owner");
  await page.getByLabel("كلمة المرور", { exact: true }).fill("A-Strong-Passphrase-401");
  await page.getByLabel("تأكيد كلمة المرور").fill("A-Strong-Passphrase-401");
  await page.getByRole("button", { name: "إنشاء الحساب" }).click();

  const originalRecoveryCode = await page.getByRole("status", { name: "رمز الاسترداد" }).textContent();
  expect(originalRecoveryCode).toMatch(/^[A-F0-9]{8}(-[A-F0-9]{8}){3}$/);
  await expect(page.getByRole("button", { name: "متابعة" })).toBeDisabled();
  await page.reload();
  await expect(page.getByRole("heading", { name: "الرئيسية" })).toBeVisible();
  await page.getByRole("link", { name: "الإعدادات" }).first().click();
  await expect(page.getByRole("heading", { name: "الإعدادات" })).toBeVisible();
  await page.locator("form").first().getByLabel("كلمة المرور الحالية").fill("A-Strong-Passphrase-401");
  await page.getByRole("button", { name: "إنشاء رمز استرداد جديد" }).click();
  await expect(page.getByRole("heading", { name: "احفظ رمز الاسترداد" })).toBeVisible();
  const recoveryCode = await page.getByRole("status", { name: "رمز الاسترداد" }).textContent();
  expect(recoveryCode).toMatch(/^[A-F0-9]{8}(-[A-F0-9]{8}){3}$/);
  expect(recoveryCode).not.toBe(originalRecoveryCode);
  await page.getByLabel("حفظت رمز الاسترداد في مكان آمن").check();
  await page.getByRole("button", { name: "متابعة" }).click();
  await expect(page.getByRole("heading", { name: "الإعدادات" })).toBeVisible();

  await page.getByRole("button", { name: "تسجيل الخروج" }).click();
  await expect(page.getByRole("heading", { name: "تسجيل الدخول" })).toBeVisible();
  await page.getByLabel("اسم المستخدم").fill("owner");
  await page.getByLabel("كلمة المرور", { exact: true }).fill("Wrong-Password-Not-Valid");
  await page.getByRole("button", { name: "دخول" }).click();
  const invalidCredentials = await page.getByRole("alert").innerText();
  expect(invalidCredentials).toContain("اسم المستخدم أو كلمة المرور غير صحيحة.");
  expect(invalidCredentials).not.toMatch(/[A-Za-z]/);

  await page.getByLabel("كلمة المرور", { exact: true }).fill("A-Strong-Passphrase-401");
  await page.getByRole("button", { name: "دخول" }).click();
  await expect(page.getByRole("heading", { name: "الرئيسية" })).toBeVisible();
  await page.getByRole("button", { name: "تسجيل الخروج" }).click();
  await page.getByRole("button", { name: "نسيت كلمة المرور؟" }).click();
  await page.getByLabel("رمز الاسترداد").fill(recoveryCode!);
  await page.getByLabel("كلمة المرور الجديدة").fill("A-New-Strong-Passphrase-802");
  await page.getByLabel("تأكيد كلمة المرور").fill("A-New-Strong-Passphrase-802");
  await page.getByRole("button", { name: "إعادة تعيين كلمة المرور" }).click();
  await expect(page.getByRole("heading", { name: "احفظ رمز الاسترداد" })).toBeVisible();
  await page.getByLabel("حفظت رمز الاسترداد في مكان آمن").check();
  await page.getByRole("button", { name: "متابعة" }).click();
  await expect(page.getByRole("heading", { name: "الرئيسية" })).toBeVisible();
});

test("validation, not-found, server-stopped, and internal failures stay Arabic", async ({ browser }) => {
  const context: BrowserContext = await browser.newContext({ baseURL: baseUrl });
  const page = await context.newPage();
  const visibleAlert = async () => {
    const text = await page.getByRole("alert").innerText();
    expect(text).not.toMatch(/[A-Za-z]/);
    return text;
  };

  await page.route("**/api/v1/bootstrap", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        setupRequired: true,
        authenticated: false,
        username: null,
        recoveryCodeAcknowledgementRequired: false,
        launchToken: "validation-test-token",
        inactivityTimeoutMinutes: 30,
      }),
    });
  });
  await page.route("**/api/v1/auth/setup", async (route) => {
    await route.fulfill({
      status: 422,
      contentType: "application/json",
      body: JSON.stringify({
        code: "VALIDATION_FAILED",
        correlationId: "validation-test",
        errors: [{ field: "ConfirmPassword", code: "PASSWORD_MISMATCH" }],
      }),
    });
  });
  await page.goto(baseUrl);
  await page.getByLabel("اسم المستخدم").fill("owner");
  await page.getByLabel("كلمة المرور", { exact: true }).fill("A-Strong-Passphrase-401");
  await page.getByLabel("تأكيد كلمة المرور").fill("A-Different-Passphrase-401");
  await page.getByRole("button", { name: "إنشاء الحساب" }).click();
  expect(await visibleAlert()).toContain("تأكيد كلمة المرور");
  await page.unroute("**/api/v1/auth/setup");
  await page.unroute("**/api/v1/bootstrap");

  const realNotFound = await page.request.get(`${baseUrl}/api/v1/no-such-browser-route`);
  expect(realNotFound.status()).toBe(404);
  expect((await realNotFound.json()).code).toBe("NOT_FOUND");

  await page.route("**/api/v1/bootstrap", async (route) => {
    await route.fulfill({
      status: 404,
      contentType: "application/json",
      body: JSON.stringify({ code: "NOT_FOUND", correlationId: "test-404", errors: [] }),
    });
  });
  await page.goto(baseUrl);
  expect(await visibleAlert()).toContain("المطلوب غير موجود.");
  await page.unroute("**/api/v1/bootstrap");

  await page.route("**/api/v1/bootstrap", async (route) => {
    await route.fulfill({
      status: 500,
      contentType: "application/json",
      body: JSON.stringify({ code: "INTERNAL_ERROR", correlationId: "test-500", errors: [] }),
    });
  });
  await page.reload();
  expect(await visibleAlert()).toContain("حدث خطأ داخلي.");
  await page.unroute("**/api/v1/bootstrap");

  await page.route("**/api/v1/bootstrap", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        setupRequired: true,
        authenticated: false,
        username: null,
        recoveryCodeAcknowledgementRequired: false,
        launchToken: "server-stopped-test-token",
        inactivityTimeoutMinutes: 30,
      }),
    });
  });
  await page.goto(baseUrl);
  await expect(page.getByRole("heading", { name: "إعداد حساب المالك" })).toBeVisible();
  await page.unroute("**/api/v1/bootstrap");
  apiProcess.kill();
  await once(apiProcess, "exit");
  await page.getByLabel("اسم المستخدم").fill("owner");
  await page.getByLabel("كلمة المرور", { exact: true }).fill("A-Strong-Passphrase-401");
  await page.getByLabel("تأكيد كلمة المرور").fill("A-Strong-Passphrase-401");
  await page.getByRole("button", { name: "إنشاء الحساب" }).click();
  expect(await visibleAlert()).toContain("تعذر الاتصال بالتطبيق المحلي.");

  await context.close();
});
