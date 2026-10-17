# ADR 0057: The sharing page, the teacher cards and «شاشة العرض»

- Status: accepted (owner-approved Phase 9 design; milestone 9a-2, branch `work/phase-9a`)
- Date: 2026-10-17
- Related: ADR 0056 (packages and the school key), ADR 0054 (printing without a browser margin), ADR 0052 (launcher and WebView2)

## Context

9a-1 made signed packages. The owner now needs to hand them out: a QR on the screen for a teacher standing at the desk, files for
WhatsApp and Telegram, printed cards for the whole staff, and a screen in the staff room that shows what is happening now. All of
it must stay offline, Arabic, accessible, and must not weaken the local-only security model.

## Decision

1. **«الجدول ← مشاركة مع الهاتف»** (`/timetable/share`, a tab of «الجدول»): the approved version («الإصدار N — بتاريخ …») and its
   expiry, the school key (fingerprint, the password-protected copy, import, recreation), the pairing code, one teacher's code or
   file, all teachers as a zip, the principal file with the privacy warning, the teacher cards and the screen.
2. **One QR encoder, on the server** (QRCoder 1.6.0, managed, MIT; Infrastructure only). The API returns the module matrix and the
   browser draws one SVG path, always dark on white. Level M; a code needing a version above 25 (997 bytes) is not offered: the page
   says «استخدم ملف .sst لهذا المعلم». Tests decode every code with ZXing.Net (tests only) and read the package back.
3. **Teacher cards**: A4 portrait, four cards a sheet (2 × 2), never split; each card has the teacher's name, two numbered codes
   «١ تحميل التطبيق» (the link, 2.2 cm) and «٢ جدولي» (the teacher's package at 6 cm, which also pairs the phone: one scan,
   DECISIONS_LOG #111), the optional pairing code small (1.8 cm) and labelled «رمز ربط المدرسة (اختياري)», the year, the version and
   date, and the key fingerprint. The link is a setting (`SharingSettings`, https only, migration `Phase9SharingSettings`).
   Printed with `window.print()` like the timetables (no browser margin).
4. **«شاشة العرض»** (`/kiosk`, outside the login and the shell): every section's current and next period with subject and teacher,
   today's clock (semester and session aware, from the same day resolution as the phone, `PackageCalendar`), «عطلة: …», «لا دوام
   اليوم», «خارج الدوام». It refreshes every minute and needs no interaction; light or dark; reduced motion respected.
   The TV must be connected to the school computer itself (HDMI, an extended display): the program listens on 127.0.0.1 only, so a
   smart TV or another computer on the network cannot open the screen (by design).
   It reads `GET /api/v1/kiosk/today` with its **own key**: random, in memory only, made by «فتح شاشة العرض» (owner session + launch
   token), valid until the program closes or «إيقاف شاشة العرض». The key travels in the address fragment and is removed from the
   address bar at once. The screen never uses or extends the owner's session, so the owner's inactivity lock still applies.
5. **The launcher** opens `/kiosk` in its own borderless full-screen window on the second screen when one is connected (a TV as an
   extended display), sharing the main window's WebView2 profile; it shows `/kiosk` only, Esc or «إغلاق» closes it. A plain
   browser opens a tab with a «ملء الشاشة» button.
6. **Audit**: every code shown (`SharingPairingShown`, `SharingTeacherQrShown`), every file, the cards when prepared for printing
   (`SharingCardsPrinted`; the server cannot know whether paper came out), the link (`SharingSettingsUpdated`) and the screen
   (`SharingKioskStarted`).

## Consequences

- New dependency QRCoder (Infrastructure; `ArchitectureTests`) and ZXing.Net (tests only).
- The Arabic dictionary allows the codes QR, USB, A4, sst, sstkey and zip (`noLatinText.test.ts`); error messages stay Latin-free.
- The kiosk shows the approved version even when today's data has changed (it is what the teachers carry on their phones).
- Version 1.1.0; the installer upgrades 1.0.0 in place (same AppId); the published 1.0.0 files are not touched.
