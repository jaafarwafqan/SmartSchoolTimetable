# The .sst phone package format (version 1)

Status: Phase 9a-1 and 9a-2 (one scan, holiday codes), ADR 0056 and ADR 0057. This document is the contract between the desktop app (writer) and the phone app
«الجدول الذكي — المعلم» (reader, repository `SmartSchoolTimetable-Mobile`). Both repositories test against the same
files in `tests/vectors/`.

## 1. Why packages

The desktop app stays offline and listens on `127.0.0.1` only. A phone never connects to it. The timetable reaches a phone
as a self-contained, signed package, carried by a QR code (teachers) or by a `.sst` file sent over WhatsApp, Telegram or USB
(teachers and the principal). The signature lets the phone prove that a package was made by its school and was not changed.

## 2. Kinds

| Kind | Value | Content | Signed with |
|---|---|---|---|
| Pairing («رمز ربط المدرسة», optional) | 0 | school id, school name, public key | the key it carries (self-signed) |
| Teacher | 1 | the school's public key, one teacher's lessons, the school's times and calendar | the school key |
| Principal | 2 | the school's public key, every section, teacher and lesson | the school key |

**One scan** (DECISIONS_LOG #111): every package carries the school id, name and public key. On a phone that is not paired, the
first valid package of any kind pairs it, trust on first use: the app shows the school name and the key fingerprint
`XXXX-XXXX-XXXX-XXXX` (the same the desktop shows) and stores the school only when the user confirms. From then on it accepts only
packages of that school id signed by that key; another key of the same school is «تغيّر مفتاح المدرسة» and needs the user to re-pair
with a confirmation. The separate pairing code stays available (on the screen, as `school-pairing.sst`, and small on the teacher
cards) to pair a phone before it has a timetable or after the key was recreated.

## 3. Envelope (binary)

```
offset  size  field
0       3     magic "SST" (0x53 0x53 0x54)
3       1     envelope version = 1
4       64    Ed25519 signature (RFC 8032, pure Ed25519)
68      n     body: raw DEFLATE (RFC 1951, no zlib/gzip header) of the CBOR payload
```

- **Signed message** = bytes 0–3 (magic and envelope version) followed by the body, i.e. the whole file without the signature.
- **QR text** = the whole envelope as base64url (RFC 4648 §5) **without padding**. A teacher QR uses error correction level M.
- A `.sst` file holds the envelope bytes as they are.
- Reader limits: the envelope is at most 256 KiB; the inflated payload at most 2 MiB (stop inflating beyond it).

## 4. Payload (CBOR, RFC 8949)

A single definite-length map with small unsigned integer keys, written in canonical form (shortest integers, keys
ascending). **Readers ignore keys they do not know, and fields added at the end of a row**; adding either never needs a new
format version. The format version
(key 0) changes only when an existing key changes meaning or shape.

| Key | Field | CBOR type | Kinds | Notes |
|---|---|---|---|---|
| 0 | format version | uint | all | `1`. A reader refuses a higher value («حدّث التطبيق») before reading anything else. |
| 1 | kind | uint | all | 0 pairing, 1 teacher, 2 principal |
| 2 | school id | bstr(16) | all | random UUID, RFC 4122 byte order (big-endian). Kept when the key is recreated. |
| 3 | school name | tstr | all | |
| 4 | key id | bstr(8) | all | first 8 bytes of SHA-256 of the signing public key |
| 5 | generated at | uint | all | Unix seconds, UTC |
| 6 | public key | bstr(32) | all | Ed25519 public key of the signer; its SHA-256 must start with key 4 (one scan). A reader accepts a timetable package without it only on a paired phone. |
| 10 | academic year | tstr | 1, 2 | e.g. `2026-2027` |
| 11 | timetable version | uint | 1, 2 | the approved version number («الإصدار N») |
| 12 | approved at | uint | 1, 2 | Unix seconds, UTC («بتاريخ …») |
| 13 | expires on | uint | 1, 2 | days since 1970-01-01; the last valid day (the end of the academic year) |
| 14 | time zone | tstr | 1, 2 | IANA id (e.g. `Asia/Baghdad`); every date and clock time in the package is local to it |
| 15 | year start | uint | 1, 2 | days since 1970-01-01 |
| 16 | year end | uint | 1, 2 | days since 1970-01-01 |
| 17 | semesters | array of `[start, end]` | 1, 2 | in date order, at most two; days since 1970-01-01 |
| 18 | days without lessons | array of `[start, end, title, flags, kind]` or `[start, end, "", flags, kind, code]` | 1, 2 | every enabled calendar entry that affects the timetable; inclusive dates; flags bit 0 = approximate (Hijri by calculation, may move a day or two), bit 1 = by decision (may be announced yearly); kind 0 official holiday, 1 school holiday, 2 exam, 3 special day (an unknown kind reads as 0); `code` (§4.1) replaces the title of an unchanged Iraqi-template holiday |
| 19 | working days | array of uint | 1, 2 | ISO weekday, 1 = Monday … 7 = Sunday |
| 20 | week start | uint | 1, 2 | ISO weekday (display order only) |
| 21 | sessions | array of `[session, periods]` | 1, 2 | session 1 = the school's own timing (morning; a single-session school's only timing), 2 = noon, 3 = evening. `periods` = array of `[kind, start, end]`, kind 0 lesson / 1 break, minutes after local midnight, in time order |
| 22 | session days | array of `[semester, day, session]` | 1, 2 | double shift only; empty for a single session (every day uses session 1) |
| 23 | subjects | array of `[name, colour index]` | 1, 2 | colour index 1–10 of the desktop palette |
| 24 | sections | array of `[stage name, label]` | 1, 2 | e.g. `["الأول المتوسط", "أ"]` |
| 25 | teachers | array of `[name, short name]` | 1, 2 | teacher package: exactly one; principal: every active teacher (some may have no lessons) |
| 26 | lessons | array of `[day, lesson, subject, section, teacher]` | 1, 2 | ISO day; lesson number 1..N counting lessons only (breaks are not numbered); the last three are indexes into keys 23, 24 and 25. Sorted by day, lesson, section. |

### 4.1 Holiday codes

A calendar entry that came from the program's Iraqi holiday template, with its title unchanged, travels as a small code (title
empty, code as the sixth field) instead of its Arabic title: the titles were a quarter of a full-time teacher's package and pushed
it past one QR code (DECISIONS_PENDING #115). The phone bundles the same table, `tests/vectors/holiday-codes.json` (code, template
key, Arabic title); codes are fixed per key and never renumbered. A code the phone does not know (a newer desktop) is shown as
«عطلة» with no title. A renamed or hand-made entry always keeps its own title.

| Code | Key | Title |
|---|---|---|
| 1 | new-year | رأس السنة الميلادية |
| 2 | army-day | عيد الجيش العراقي |
| 3 | nowruz | عيد نوروز |
| 4 | labour-day | عيد العمال |
| 5 | christmas | عيد الميلاد المجيد |
| 6 | revolution-14-july | ثورة ١٤ تموز |
| 7 | national-day | اليوم الوطني العراقي |
| 8 | victory-day | عيد النصر |
| 9 | hijri-new-year | رأس السنة الهجرية |
| 10 | ashura | يوم عاشوراء |
| 11 | mawlid | المولد النبوي الشريف |
| 12 | eid-al-fitr | عيد الفطر المبارك |
| 13 | eid-al-adha | عيد الأضحى المبارك |
| 14 | ghadir | عيد الغدير |

### Resolving a lesson's clock time (the phone)

1. **Semester**: the semester (key 17) whose dates contain the date. A date outside both semesters uses semester 1 (the
   desktop viewer does the same).
2. **Session**: the `[semester, day, session]` row of key 22; none (single session, or an unmapped day) means session 1.
3. **Time**: in that session's periods (key 21), take the lesson rows (kind 0) only; lesson number `n` is the n-th of them.
   Break rows are shown as «استراحة» between lessons.
4. A date inside a key-18 entry (a holiday, or an exam or special day marked as affecting the timetable) or not a working day (key 19) has no lessons and no notifications; a date outside
   [year start, year end] is outside the academic year (no lessons, no notifications).

## 5. Reading a package (the order matters)

| Step | Check | Result if it fails |
|---|---|---|
| 1 | length > 68, ≤ 256 KiB, magic `SST` | **malformed** («هذا الملف ليس ملف جدول») |
| 2 | envelope version: greater than 1 | **newer format** («حدّث التطبيق») |
| | envelope version: not 1 | malformed |
| 3 | inflate (≤ 2 MiB) and read key 0: greater than supported | **newer format** |
| 4 | decode the payload (any error, a missing key, an index out of range) | **tampered** |
| 5 | a carried key (key 6) has key id = SHA-256(key)[0..8]; a pairing code carries one; a timetable kind has its timetable | tampered |
| 6 | **not paired** (or a pairing code of another school): the package carries key 6 | **not paired** («امسح رمز ربط المدرسة أولاً») |
| 6a | … and the signature verifies with the carried key | tampered |
| 6b | … and today ≤ expires on (a pairing code has no expiry) | expired (no pairing) |
| 6c | … then **valid, proposing the school** (id, name, key) for trust: the phone confirms the name and fingerprint, then stores it | — |
| 7 | paired: school id = the paired school's | **wrong school** («هذا الجدول لمدرسة أخرى») |
| 8 | key id = the paired key's | **key changed** («تغيّر مفتاح المدرسة: امسح رمز الربط الجديد») |
| 9 | the signature verifies with the paired key | **tampered** («الملف معدّل أو تالف») |
| 10 | today (in the package time zone) ≤ expires on | **expired** («انتهت صلاحية هذا الجدول») |

Steps 3–5 read unverified bytes; that is safe because of the size limits and because nothing is shown or stored before the
signature check (6a or 9). Trust on first use means a phone that has never seen the school accepts the first validly signed
package it is given; the name and fingerprint shown before saving are the user's check (the owner's decision). A newer timetable version replaces an older one on the phone; an older or equal one is refused with an
explanation (phone rule, B2).

The C# reference reader is `PhonePackageReader` (Infrastructure); the desktop never imports packages.

## 6. Keys

- **Algorithm**: Ed25519. Desktop: BouncyCastle (managed). Phone: the `cryptography` Dart package.
- **School key**: made on first use (the first pairing code or export). The 32-byte private seed is protected with Windows
  DPAPI (current user, fixed application entropy) and stored in the `SchoolKeys` table, so every backup carries it.
- **Restored elsewhere**: a backup restored under another Windows account or on another computer cannot read the protected
  seed. The desktop says so (`SHARE_KEY_UNREADABLE`) and first offers «استيراد مفتاح المدرسة» from its saved copy (§6.1), which
  keeps the school id and the phones' trust; only without a copy is «إعادة إنشاء مفتاح المدرسة» left, after which every phone must
  scan the new pairing code (the same as recreating the key on purpose, two confirmations).
- **Recreated key**: same school id, new key, generation + 1. A phone that still trusts the old key sees **key changed**
  and is asked to re-pair (with confirmation; one school per phone).
- The private seed never leaves the desktop except inside its password-protected copy, is never logged and never appears in
  an API response.

### 6.1 The saved copy of the key (`.sstkey`, DECISIONS_LOG #106)

```
offset  size  field
0       4     magic "SSTK"
4       1     version = 1
5       1     KDF = 1 (PBKDF2-HMAC-SHA256)
6       4     iterations, uint32 big-endian (600 000; a reader refuses fewer, or more than 10 000 000)
10      16    salt (random)
26      16    password check = HMAC-SHA256(check key, "SmartSchoolTimetable/sstkey/password-check")[0..16]
42      12    AES-GCM nonce (random)
54      92    ciphertext
146     16    AES-GCM tag; additional data = bytes 0..53
```

PBKDF2 over the password (UTF-8, Unicode NFC) gives 64 bytes: the first 32 are the AES-256-GCM key, the last 32 the check key.
A wrong password fails the check («كلمة مرور ملف المفتاح غير صحيحة»); a changed nonce, ciphertext or tag fails the tag («المحتوى
عُدّل أو تلف»). Plaintext: school id (16, RFC 4122) | generation (int32 BE) | created at (int64 BE, Unix seconds) | public key (32) |
private seed (32); the seed must give the public key. The password follows the account rules (8–1024 characters, typed twice).
Importing puts the file's school id, generation and key back; replacing a different key that still works on this computer, or a
key of another school id, needs a confirmation (`SHARE_KEY_IMPORT_CONFIRMATION_REQUIRED`).

## 7. What the desktop exports

Only the **approved** version of the **current** academic year, and only while it does not conflict with today's school
data (the same check that blocks approval, `SHARE_TIMETABLE_CONFLICTS`), and not after the year has ended
(`SHARE_YEAR_ENDED`). The lessons, names and the morning timing come from the version's own snapshot; the other sessions'
timing and the day → session mapping are today's settings (they never change a lesson, DECISIONS_PENDING #81). Key 18 holds
every enabled calendar entry that affects the timetable and overlaps the year, with its kind (DECISIONS_LOG #107).

| Endpoint (`/api/v1/sharing`) | Result | Audit |
|---|---|---|
| `GET /` | key (school id, fingerprint, generation, readable), approved version (number, approval date, conflicts, expiry, year ended), teachers with lessons | — |
| `POST /pairing.sst` | `school-pairing.sst` | `SharingPairingExported` (and `SharingKeyCreated` the first time) |
| `POST /teachers/{id}/package.sst` | `<teacher name>-v<N>.sst` (name made safe for Windows, Android and iOS, DECISIONS_LOG #108) | `SharingTeacherExported` |
| `POST /teachers.zip` | `teachers-v<N>.zip`, one `.sst` per teacher with lessons | `SharingAllTeachersExported` |
| `POST /principal.sst` | `principal-v<N>.sst` (holds every teacher's name: share it privately) | `SharingPrincipalExported` |
| `POST /key/backup.sstkey` `{password, confirmPassword}` | `school-key-<fingerprint>.sstkey` | `SharingKeyBackedUp` |
| `POST /key/import` `{file (base64), password, confirmReplace}` | `{key, changed}` | `SharingKeyImported` |
| `POST /key/regenerate` `{confirm, confirmRepair}` | the new key | `SharingKeyRegenerated` |

Every export is a POST (it signs and writes an audit entry), so it carries the per-launch token.

## 8. Size

With the public key inside (one scan) and the holiday codes, the vector teacher package is 879 QR characters (version 24 at level
M). Measured full-time teachers with the program's whole Iraqi calendar for 2026-2027, double shift and 30 lessons
(`PhonePackageCodecTests`): 6 sections × 1 subject 896 characters (version 24), 6 × 2 910 (24), 4 × 2 912 (25); a teacher spread over
11 sections and 4 subjects needs 1015 (version 26) and gets the file. The desktop offers a QR up to version 25 at level M (997 characters, `SharingService.MaxQrVersion`); above it the sharing
page shows «استخدم ملف .sst لهذا المعلم» instead of a code too dense to scan reliably. On the printed card the teacher's code is
6 cm wide.

## 8.1 «شاشة العرض» and the day rules

`PackageCalendar.Resolve` (Application) is the C# form of §4 "Resolving a lesson's clock time": the staff-room screen
(`GET /api/v1/kiosk/today`, ADR 0057) uses it, and its tests (`SharingScreensTests`) pin the same cases the phone must pass.

## 9. Test vectors

`tests/vectors/` (see its README): pairing, valid teacher and principal packages with their expected decoding as JSON,
tampered, wrong school, key changed (paired phone, another key), expired, future format, first scan (unpaired phone, pairs), not
paired (no key carried), a carried key that did not sign, the holiday code table (`holiday-codes.json`), a manifest (`vectors.json`) with the expected
result and the "today" to use, and `SHA256SUMS`. All data is fictional and the three keys are test keys derived from public
strings. `SstVectorTests` checks them; `WRITE_SST_VECTORS=1` regenerates them after a deliberate format change.
