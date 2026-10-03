"use strict";

const content = document.querySelector("#content");
const notice = document.querySelector("#notice");
const tokenHeader = "X-Local-Launch-Token";
let launchToken = "";
let inactivityTimeoutMinutes = 30;
let idleTimer = 0;
let lastActivityPing = 0;

async function request(path, method = "GET", body) {
  const headers = new Headers();
  if (body !== undefined) {
    headers.set("Content-Type", "application/json");
    headers.set(tokenHeader, launchToken);
  }
  const response = await fetch(path, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    credentials: "same-origin",
    cache: "no-store"
  });
  if (response.status === 204) return { response };
  const data = await response.json().catch(() => ({}));
  return { response, data };
}

function showNotice(message, kind = "") {
  notice.textContent = message;
  notice.className = `notice ${kind}`.trim();
  notice.hidden = !message;
}

function field(id, label, type = "text", autocomplete = "") {
  const wrapper = document.createElement("div");
  wrapper.className = "field";
  const labelElement = document.createElement("label");
  labelElement.htmlFor = id;
  labelElement.textContent = label;
  const input = document.createElement("input");
  input.id = id;
  input.name = id;
  input.type = type;
  input.required = true;
  input.autocomplete = autocomplete;
  if (type === "password") input.minLength = 12;
  wrapper.append(labelElement, input);
  return wrapper;
}

function button(text, onClick, secondary = false) {
  const element = document.createElement("button");
  element.type = "button";
  element.textContent = text;
  if (secondary) element.className = "secondary";
  element.addEventListener("click", onClick);
  return element;
}

function actions(...elements) {
  const wrapper = document.createElement("div");
  wrapper.className = "actions";
  wrapper.append(...elements);
  return wrapper;
}

function renderRecoveryCode(code, message) {
  content.replaceChildren();
  const title = document.createElement("h2");
  title.textContent = "احفظ رمز الاسترداد";
  const description = document.createElement("p");
  description.textContent = message;
  description.className = "warning";
  const codeElement = document.createElement("code");
  codeElement.className = "recovery-code";
  codeElement.textContent = code;
  const instruction = document.createElement("p");
  instruction.textContent = "لن يُعرض هذا الرمز مرة أخرى. خزّنه في مكان آمن أو اطبعه الآن.";
  const confirm = button("حفظت الرمز بأمان", async () => {
    showNotice("يمكنك الآن تسجيل الدخول باستخدام كلمة المرور الجديدة.", "success");
    renderLogin();
  });
  content.append(title, description, codeElement, instruction, actions(confirm));
  codeElement.focus();
}

function renderSetup() {
  showNotice("إنشاء حساب المالك المحلي لأول مرة. رمز الاسترداد هو المسار الوحيد لإعادة تعيين كلمة المرور.", "");
  const heading = document.createElement("h2");
  heading.textContent = "إعداد حساب المالك";
  const form = document.createElement("form");
  form.append(
    field("username", "اسم المستخدم", "text", "username"),
    field("password", "كلمة المرور (12 محرفاً على الأقل)", "password", "new-password")
  );
  const submit = document.createElement("button");
  submit.type = "submit";
  submit.textContent = "إنشاء الحساب";
  form.append(actions(submit));
  form.addEventListener("submit", async event => {
    event.preventDefault();
    showNotice("");
    try {
      const data = Object.fromEntries(new FormData(form));
      const { response, data: result } = await request("/api/v1/auth/setup", "POST", data);
      if (!response.ok) {
        showNotice(result.detail || "تعذر إنشاء الحساب.", "error");
        return;
      }
      renderRecoveryCode(result.recoveryCode, "رمز الاسترداد هو وسيلتك الوحيدة لإعادة تعيين كلمة المرور.");
    } catch {
      showNotice("تعذر الاتصال بالتطبيق المحلي.", "error");
    }
  });
  content.replaceChildren(heading, form);
}

function renderLogin(message = "") {
  showNotice(message);
  const heading = document.createElement("h2");
  heading.textContent = "تسجيل الدخول";
  const form = document.createElement("form");
  form.append(
    field("username", "اسم المستخدم", "text", "username"),
    field("password", "كلمة المرور", "password", "current-password")
  );
  const submit = document.createElement("button");
  submit.type = "submit";
  submit.textContent = "دخول";
  form.append(actions(submit));
  form.addEventListener("submit", async event => {
    event.preventDefault();
    showNotice("");
    try {
      const { response, data } = await request(
        "/api/v1/auth/login",
        "POST",
        Object.fromEntries(new FormData(form))
      );
      if (!response.ok) {
        showNotice(data.detail || "تعذر تسجيل الدخول.", "error");
        return;
      }
      await checkSession();
    } catch {
      showNotice("تعذر الاتصال بالتطبيق المحلي.", "error");
    }
  });
  const recover = button("نسيت كلمة المرور؟", renderRecovery, true);
  recover.className = "link-button";
  content.replaceChildren(heading, form, actions(recover));
}

function renderRecovery() {
  showNotice("رمز الاسترداد هو المسار الوحيد لإعادة تعيين كلمة المرور. إذا فقدته مع كلمة المرور فلا توجد طريقة لاستعادة الحساب.");
  const heading = document.createElement("h2");
  heading.textContent = "استرداد الحساب";
  const form = document.createElement("form");
  const code = field("recoveryCode", "رمز الاسترداد", "text", "off");
  code.querySelector("input").dir = "ltr";
  form.append(
    code,
    field("newPassword", "كلمة المرور الجديدة (12 محرفاً على الأقل)", "password", "new-password")
  );
  const submit = document.createElement("button");
  submit.type = "submit";
  submit.textContent = "إعادة تعيين كلمة المرور";
  form.append(actions(submit, button("عودة لتسجيل الدخول", renderLogin, true)));
  form.addEventListener("submit", async event => {
    event.preventDefault();
    showNotice("");
    try {
      const data = Object.fromEntries(new FormData(form));
      const { response, data: result } = await request("/api/v1/auth/recovery", "POST", data);
      if (!response.ok) {
        showNotice(result.detail || "تعذر استرداد الحساب.", "error");
        return;
      }
      renderRecoveryCode(result.recoveryCode, "تم تغيير كلمة المرور واستهلاك الرمز السابق. هذا رمزك الجديد الوحيد للاسترداد.");
    } catch {
      showNotice("تعذر الاتصال بالتطبيق المحلي.", "error");
    }
  });
  content.replaceChildren(heading, form);
}

async function lockForInactivity() {
  window.clearTimeout(idleTimer);
  try {
    await request("/api/v1/auth/logout", "POST", {});
  } finally {
    renderLogin("انتهت جلسة الدخول بسبب عدم النشاط. سجّل الدخول للمتابعة.");
  }
}

function resetIdleTimer() {
  window.clearTimeout(idleTimer);
  idleTimer = window.setTimeout(lockForInactivity, inactivityTimeoutMinutes * 60_000);
}

async function recordActivity() {
  resetIdleTimer();
  const now = Date.now();
  if (now - lastActivityPing < 60_000) return;
  lastActivityPing = now;
  try {
    const { response } = await request("/api/v1/private/status");
    if (response.status === 401) {
      await lockForInactivity();
    }
  } catch {
    showNotice("تعذر تجديد الجلسة عبر الخادم المحلي.", "error");
  }
}

function renderAuthenticated(username) {
  showNotice("");
  const heading = document.createElement("h2");
  heading.textContent = `مرحباً ${username}`;
  const description = document.createElement("p");
  description.textContent = "تم تسجيل الدخول إلى التطبيق المحلي. تبدأ ميزات إدارة الجداول في المراحل التالية.";

  const formHeading = document.createElement("h2");
  formHeading.textContent = "تغيير كلمة المرور";
  const form = document.createElement("form");
  form.append(
    field("currentPassword", "كلمة المرور الحالية", "password", "current-password"),
    field("newPassword", "كلمة المرور الجديدة (12 محرفاً على الأقل)", "password", "new-password")
  );
  const submit = document.createElement("button");
  submit.type = "submit";
  submit.textContent = "تغيير كلمة المرور";
  form.append(actions(submit));
  form.addEventListener("submit", async event => {
    event.preventDefault();
    showNotice("");
    try {
      const { response, data } = await request(
        "/api/v1/auth/change-password",
        "POST",
        Object.fromEntries(new FormData(form))
      );
      if (!response.ok) {
        showNotice(data.detail || "تعذر تغيير كلمة المرور.", "error");
        return;
      }
      await lockForInactivity();
      showNotice("تم تغيير كلمة المرور. سجّل الدخول باستخدام الكلمة الجديدة.", "success");
    } catch {
      showNotice("تعذر الاتصال بالتطبيق المحلي.", "error");
    }
  });
  const logout = button("تسجيل الخروج", async () => {
    await lockForInactivity();
  }, true);
  content.replaceChildren(heading, description, formHeading, form, actions(logout));
  resetIdleTimer();
}

async function checkSession() {
  const { response, data } = await request("/api/v1/bootstrap");
  if (!response.ok) throw new Error("Bootstrap failed.");
  if (data.setupRequired) {
    renderSetup();
  } else if (data.authenticated) {
    renderAuthenticated(data.username);
  } else {
    renderLogin();
  }
}

async function start() {
  try {
    const { response, data } = await request("/api/v1/bootstrap");
    if (!response.ok) throw new Error("Bootstrap failed.");
    launchToken = data.launchToken;
    inactivityTimeoutMinutes = data.inactivityTimeoutMinutes;
    if (data.setupRequired) renderSetup();
    else if (data.authenticated) renderAuthenticated(data.username);
    else renderLogin();
  } catch {
    showNotice("تعذر الاتصال بالخادم المحلي. تأكد من تشغيل التطبيق عبر عنوانه المحلي.", "error");
    content.replaceChildren();
  }
}

for (const eventName of ["pointerdown", "keydown", "touchstart"]) {
  document.addEventListener(eventName, () => {
    if (document.querySelector("#currentPassword")) void recordActivity();
  }, { passive: true });
}

start();
