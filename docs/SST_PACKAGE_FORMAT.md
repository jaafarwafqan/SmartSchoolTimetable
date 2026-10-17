# The .sst phone package format (version 1)

Status: Phase 9a-1, ADR 0056. This document is the contract between the desktop app (writer) and the phone app
«الجدول الذكي — المعلم» (reader, repository `SmartSchoolTimetable-Mobile`). Both repositories test against the same
files in `tests/vectors/`.

## 1. Why packages

The desktop app stays offline and listens on `127.0.0.1` only. A phone never connects to it. The timetable reaches a phone
as a self-contained, signed package, carried by a QR code (teachers) or by a `.sst` file sent over WhatsApp, Telegram or USB
(teachers and the principal). The signature lets the phone prove that a package was made by its school and was not changed.

## 2. Kinds

| Kind | Value | Content | Signed with |
|---|---|---|---|
| Pairing («رمز ربط المدرسة») | 0 | school id, school name, public key | the key it carries (self-signed) |
| Teacher | 1 | one teacher's lessons, with the school's times and calendar | the school key |
| Principal | 2 | every section, teacher and lesson | the school key |

A phone scans the pairing code once (trust on first use: the app shows the school name and the key fingerprint
`XXXX-XXXX-XXXX-XXXX` for confirmation, the same fingerprint the desktop shows). From then on it accepts only packages of
that school id signed by that key. A pairing package may also be sent as a file (`school-pairing.sst`).

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
ascending). **Readers ignore keys they do not know**; adding a key never needs a new format version. The format version
(key 0) changes only when an existing key changes meaning or shape.

| Key | Field | CBOR type | Kinds | Notes |
|---|---|---|---|---|
| 0 | format version | uint | all | `1`. A reader refuses a higher value («حدّث التطبيق») before reading anything else. |
| 1 | kind | uint | all | 0 pairing, 1 teacher, 2 principal |
| 2 | school id | bstr(16) | all | random UUID, RFC 4122 byte order (big-endian). Kept when the key is recreated. |
| 3 | school name | tstr | all | |
| 4 | key id | bstr(8) | all | first 8 bytes of SHA-256 of the signing public key |
| 5 | generated at | uint | all | Unix seconds, UTC |
| 6 | public key | bstr(32) | pairing | Ed25519 public key |
| 10 | academic year | tstr | 1, 2 | e.g. `2026-2027` |
| 11 | timetable version | uint | 1, 2 | the approved version number («الإصدار N») |
| 12 | approved at | uint | 1, 2 | Unix seconds, UTC («بتاريخ …») |
| 13 | expires on | uint | 1, 2 | days since 1970-01-01; the last valid day (the end of the academic year) |
| 14 | time zone | tstr | 1, 2 | IANA id (e.g. `Asia/Baghdad`); every date and clock time in the package is local to it |
| 15 | year start | uint | 1, 2 | days since 1970-01-01 |
| 16 | year end | uint | 1, 2 | days since 1970-01-01 |
| 17 | semesters | array of `[start, end]` | 1, 2 | in date order, at most two; days since 1970-01-01 |
| 18 | holidays | array of `[start, end, title, flags]` | 1, 2 | inclusive dates; flags bit 0 = approximate (Hijri by calculation, may move a day or two), bit 1 = by decision (may be announced yearly) |
| 19 | working days | array of uint | 1, 2 | ISO weekday, 1 = Monday … 7 = Sunday |
| 20 | week start | uint | 1, 2 | ISO weekday (display order only) |
| 21 | sessions | array of `[session, periods]` | 1, 2 | session 1 = the school's own timing (morning; a single-session school's only timing), 2 = noon, 3 = evening. `periods` = array of `[kind, start, end]`, kind 0 lesson / 1 break, minutes after local midnight, in time order |
| 22 | session days | array of `[semester, day, session]` | 1, 2 | double shift only; empty for a single session (every day uses session 1) |
| 23 | subjects | array of `[name, colour index]` | 1, 2 | colour index 1–10 of the desktop palette |
| 24 | sections | array of `[stage name, label]` | 1, 2 | e.g. `["الأول المتوسط", "أ"]` |
| 25 | teachers | array of `[name, short name]` | 1, 2 | teacher package: exactly one; principal: every active teacher (some may have no lessons) |
| 26 | lessons | array of `[day, lesson, subject, section, teacher]` | 1, 2 | ISO day; lesson number 1..N counting lessons only (breaks are not numbered); the last three are indexes into keys 23, 24 and 25. Sorted by day, lesson, section. |

### Resolving a lesson's clock time (the phone)

1. **Semester**: the semester (key 17) whose dates contain the date. A date outside both semesters uses semester 1 (the
   desktop viewer does the same).
2. **Session**: the `[semester, day, session]` row of key 22; none (single session, or an unmapped day) means session 1.
3. **Time**: in that session's periods (key 21), take the lesson rows (kind 0) only; lesson number `n` is the n-th of them.
   Break rows are shown as «استراحة» between lessons.
4. A date that is a holiday (key 18) or not a working day (key 19) has no lessons; a date outside
   [year start, year end] is outside the academic year (no lessons, no notifications).

## 5. Reading a package (the order matters)

| Step | Check | Result if it fails |
|---|---|---|
| 1 | length > 68, ≤ 256 KiB, magic `SST` | **malformed** («هذا الملف ليس ملف جدول») |
| 2 | envelope version: greater than 1 | **newer format** («حدّث التطبيق») |
| | envelope version: not 1 | malformed |
| 3 | inflate (≤ 2 MiB) and read key 0: greater than supported | **newer format** |
| 4 | decode the payload (any error, a missing key, an index out of range) | **tampered** |
| 5 | pairing: key id = SHA-256(public key)[0..8] and the signature verifies with the carried key | tampered |
| 6 | timetable: the phone is paired | **not paired** («امسح رمز ربط المدرسة أولاً») |
| 7 | school id = the paired school's | **wrong school** («هذا الجدول لمدرسة أخرى») |
| 8 | key id = the paired key's | **key changed** («تغيّر مفتاح المدرسة: امسح رمز الربط الجديد») |
| 9 | the signature verifies with the paired key | **tampered** («الملف معدّل أو تالف») |
| 10 | today (in the package time zone) ≤ expires on | **expired** («انتهت صلاحية هذا الجدول») |

Steps 3–4 read unverified bytes; that is safe because of the size limits and because nothing is shown or stored before
step 9. A newer timetable version replaces an older one on the phone; an older or equal one is refused with an
explanation (phone rule, B2).

The C# reference reader is `PhonePackageReader` (Infrastructure); the desktop never imports packages.

## 6. Keys

- **Algorithm**: Ed25519. Desktop: BouncyCastle (managed). Phone: the `cryptography` Dart package.
- **School key**: made on first use (the first pairing code or export). The 32-byte private seed is protected with Windows
  DPAPI (current user, fixed application entropy) and stored in the `SchoolKeys` table, so every backup carries it.
- **Restored elsewhere**: a backup restored under another Windows account or on another computer cannot read the protected
  seed. The desktop says so (`SHARE_KEY_UNREADABLE`) and offers «إعادة إنشاء مفتاح المدرسة»; every phone must then scan the new
  pairing code. The same happens if the key is recreated on purpose (two confirmations).
- **Recreated key**: same school id, new key, generation + 1. A phone that still trusts the old key sees **key changed**
  and is asked to re-pair (with confirmation; one school per phone).
- The private seed never leaves the desktop, is never logged and never appears in an API response.

## 7. What the desktop exports

Only the **approved** version of the **current** academic year, and only while it does not conflict with today's school
data (the same check that blocks approval, `SHARE_TIMETABLE_CONFLICTS`), and not after the year has ended
(`SHARE_YEAR_ENDED`). The lessons, names and the morning timing come from the version's own snapshot; the other sessions'
timing and the day → session mapping are today's settings (they never change a lesson, DECISIONS_PENDING #81). Holidays are
the enabled calendar entries that affect the timetable, of kind official or school holiday, overlapping the year.

| Endpoint (`/api/v1/sharing`) | Result | Audit |
|---|---|---|
| `GET /` | key (school id, fingerprint, generation, readable), approved version (number, approval date, conflicts, expiry, year ended), teachers with lessons | — |
| `POST /pairing.sst` | `school-pairing.sst` | `SharingPairingExported` (and `SharingKeyCreated` the first time) |
| `POST /teachers/{id}/package.sst` | `<teacher name>-v<N>.sst` | `SharingTeacherExported` |
| `POST /teachers.zip` | `teachers-v<N>.zip`, one `.sst` per teacher with lessons | `SharingAllTeachersExported` |
| `POST /principal.sst` | `principal-v<N>.sst` (holds every teacher's name: share it privately) | `SharingPrincipalExported` |
| `POST /key/regenerate` `{confirm, confirmRepair}` | the new key | `SharingKeyRegenerated` |

Every export is a POST (it signs and writes an audit entry), so it carries the per-launch token.

## 8. Size

A typical teacher package (30 lessons, double shift, three holidays) is about 570 bytes, 766 QR characters: a version-25
QR at level M holds 997. The sharing page (9a-2) measures each teacher's QR and shows «استخدم ملف .sst لهذا المعلم» instead
of a QR that would be too dense to scan reliably.

## 9. Test vectors

`tests/vectors/` (see its README): pairing, valid teacher and principal packages with their expected decoding as JSON,
tampered, wrong school, key changed, expired, future format and not paired, a manifest (`vectors.json`) with the expected
result and the "today" to use, and `SHA256SUMS`. All data is fictional and the three keys are test keys derived from public
strings. `SstVectorTests` checks them; `WRITE_SST_VECTORS=1` regenerates them after a deliberate format change.
