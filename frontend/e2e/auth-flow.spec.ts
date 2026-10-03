import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { formatInactivityTimeout } from "../src/lib/format";
import { ApiServer } from "./support/apiServer";

const server = new ApiServer();
let baseUrl = "";

test.beforeAll(async ({ browser }) => {
  await server.start(browser, "auth-flow");
  baseUrl = server.baseUrl;
});

test.afterAll(async () => {
  await server.stop();
});

// Both tests share one real API process and database and run in file order (fullyParallel: false).
const initialPassword = "Owner-88"; // exactly the 8-character minimum
const recoveredPassword = "A-New-Strong-Passphrase-802";
const recoveryCodePattern = /^[A-F0-9]{8}(-[A-F0-9]{8}){3}$/;
const homeHeading = messages.app.greeting("owner");

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
  await expect(page.getByRole("heading", { name: homeHeading })).toHaveCount(0);
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

  // Inactivity auto-lock is chosen in Settings, saved to the database and applied immediately.
  await page.getByLabel(messages.app.inactivityLabel).selectOption("15");
  await page.getByRole("button", { name: messages.app.saveInactivity }).click();
  await expect(page.getByRole("status").filter({ hasText: messages.app.inactivitySaved })).toBeVisible();
  await expect(page.locator(".info-row")).toContainText(formatInactivityTimeout(15));
  await page.reload();
  await expect(page.getByLabel(messages.app.inactivityLabel)).toHaveValue("15");

  await page.getByRole("button", { name: messages.app.logout }).click();
  await expect(page.getByRole("heading", { name: messages.app.loginTitle })).toBeVisible();
  await page.getByLabel(messages.app.username).fill("owner");
  await page.getByLabel(messages.app.password, { exact: true }).fill("Wrong-Password-Not-Valid");
  await page.getByRole("button", { name: messages.app.loginAction }).click();
  await expectArabicAlert(page, messages.errors.INVALID_CREDENTIALS);

  await page.getByLabel(messages.app.password, { exact: true }).fill(initialPassword);
  await page.getByRole("button", { name: messages.app.loginAction }).click();
  await expect(page.getByRole("heading", { name: homeHeading })).toBeVisible();
  await page.getByRole("button", { name: messages.app.logout }).click();

  await page.getByRole("button", { name: messages.app.recoveryLink }).click();
  await page.getByLabel(messages.app.recoveryCode).fill(recoveryCode);
  await page.getByLabel(messages.app.newPassword).fill(recoveredPassword);
  await page.getByLabel(messages.app.confirmPassword).fill(recoveredPassword);
  await page.getByRole("button", { name: messages.app.resetPassword }).click();
  await acknowledgeShownCode(page);
  await expect(page.getByRole("heading", { name: homeHeading })).toBeVisible();

  // After a completed recovery, logging out returns to the login screen rather than the recovery form.
  await page.getByRole("button", { name: messages.app.logout }).click();
  await expect(page.getByRole("heading", { name: messages.app.loginTitle })).toBeVisible();
});

test("real validation and not-found responses, mocked 500, and a stopped server stay Arabic", async ({ browser }) => {
  const context: BrowserContext = await browser.newContext({ baseURL: baseUrl });
  const page = await context.newPage();

  // Empty fields are caught in the browser and shown under each field; no request is sent.
  await page.goto(baseUrl);
  await expect(page.getByRole("heading", { name: messages.app.loginTitle })).toBeVisible();
  let loginRequests = 0;
  page.on("request", (request) => {
    if (request.url().endsWith("/api/v1/auth/login")) loginRequests += 1;
  });
  await page.getByRole("button", { name: messages.app.loginAction }).click();
  await expect(page.getByText(messages.errors.REQUIRED)).toHaveCount(2);
  await expect(page.getByLabel(messages.app.username)).toHaveAttribute("aria-invalid", "true");
  expect(loginRequests).toBe(0);

  // Real 422 from the server: the recovery form leaves password length to the API, which returns field codes.
  await page.getByRole("button", { name: messages.app.recoveryLink }).click();
  await page.getByLabel(messages.app.recoveryCode).fill("00000000-00000000-00000000-00000000");
  await page.getByLabel(messages.app.newPassword).fill("short1");
  await page.getByLabel(messages.app.confirmPassword).fill("short1");
  const validationResponse = page.waitForResponse((response) => response.url().endsWith("/api/v1/auth/recovery"));
  await page.getByRole("button", { name: messages.app.resetPassword }).click();
  expect((await validationResponse).status()).toBe(422);
  await expectArabicAlert(page, messages.errors.VALIDATION_FAILED);
  await expect(page.getByText(messages.errors.PASSWORD_TOO_SHORT)).toBeVisible();
  await expect(page.getByLabel(messages.app.newPassword)).toHaveAttribute("aria-invalid", "true");
  await page.getByRole("button", { name: messages.app.backToLogin }).click();
  await expect(page.getByRole("heading", { name: messages.app.loginTitle })).toBeVisible();

  // Real 404 from the API contract, and the real not-found page for an unknown app route.
  const realNotFound = await page.request.get(`${baseUrl}/api/v1/no-such-browser-route`);
  expect(realNotFound.status()).toBe(404);
  expect((await realNotFound.json()).code).toBe("NOT_FOUND");

  await page.getByLabel(messages.app.username).fill("owner");
  await page.getByLabel(messages.app.password, { exact: true }).fill(recoveredPassword);
  await page.getByRole("button", { name: messages.app.loginAction }).click();
  await expect(page.getByRole("heading", { name: homeHeading })).toBeVisible();
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
  await server.kill();
  await page.getByLabel(messages.app.username).fill("owner");
  await page.getByLabel(messages.app.password, { exact: true }).fill(recoveredPassword);
  await page.getByRole("button", { name: messages.app.loginAction }).click();
  await expectArabicAlert(page, messages.errors.NETWORK_ERROR);

  await context.close();
});
