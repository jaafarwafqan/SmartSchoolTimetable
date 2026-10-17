# ADR 0056: Signed offline packages for phones (.sst), with a DPAPI-protected school key

- Status: accepted (owner-approved Phase 9 design; milestone 9a-1, branch `work/phase-9a`)
- Date: 2026-10-17
- Related: docs/SST_PACKAGE_FORMAT.md, ADR 0042 (backups), ADR 0046 (one shift system), ADR 0043 (daily sessions)

## Context

Teachers and the principal want the approved timetable on their phones. The desktop app must stay local and offline,
listening on `127.0.0.1` only, with no server, cloud or internet. A phone therefore never talks to the desktop: data has to
travel as a file or a QR code, through channels the school already uses (WhatsApp, Telegram, USB, a printed card). Anyone can
forward or edit such a file, so the phone needs a way to know that a package really comes from its school and is unchanged.

## Decision

1. **Packages, not connections.** The desktop writes self-contained `.sst` packages of the approved version: a pairing code
   (school id, name, public key), one per teacher, and one for the principal. The QR form is the same bytes in base64url.
2. **Format** (docs/SST_PACKAGE_FORMAT.md): `"SST" | envelope version | Ed25519 signature | raw DEFLATE(CBOR payload)`. CBOR with
   small integer keys keeps a teacher package around 570 bytes (fits one QR at level M); readers ignore unknown keys, so
   adding fields needs no new format version; a higher format version is refused with «حدّث التطبيق».
3. **Ed25519, trust on first use.** The school key is generated on first use. A phone pairs once by scanning the pairing
   code (the school name and fingerprint are shown for confirmation) and then accepts only packages of that school id signed
   by that key. The key id in every package tells a recreated key («امسح رمز الربط الجديد») from tampering.
4. **The private key** is a 32-byte seed protected with Windows DPAPI (current user, application entropy) in the
   `SchoolKeys` table (one row, migration `Phase9SchoolKey`). Being in the database, it is in every backup. DPAPI binds it
   to the Windows account: restored elsewhere it cannot be read, the desktop says so (`SHARE_KEY_UNREADABLE`) and offers
   «إعادة إنشاء مفتاح المدرسة» (two confirmations, same school id, generation + 1, audited); every phone then re-pairs.
5. **Only the approved version of the current year**, only while it fits today's data (the approval check), only before the
   year's end. Expiry = the last day of the academic year.
6. **Libraries** (Infrastructure only, enforced by `ArchitectureTests`): `BouncyCastle.Cryptography` 2.6.2 (Ed25519 in managed
   code; .NET 9 has no Ed25519 and a native libsodium would add a native dependency to the installer), `System.Formats.Cbor`
   9.0.20 and `System.Security.Cryptography.ProtectedData` 9.0.20 (Microsoft). DEFLATE and ZIP come from the BCL.
7. **Shared test vectors** in `tests/vectors/` (fictional data, test keys from public strings) are the contract with the phone
   repository, which copies them and checks `SHA256SUMS`.

## Consequences

- Exports are POSTs with the per-launch token and an audit entry (new audit category «المشاركة مع الهاتف»).
- The school can lose its phone trust only by losing the Windows account (or by recreating the key); this is said in Arabic
  on the sharing page and in the user guide. DECISIONS_PENDING #106 records the alternative (a password-protected key export).
- The desktop never imports packages; `PhonePackageReader` exists as the reference implementation for tests and vectors.
- Names in a principal package are personal data: the page warns «يحتوي أسماء كل المعلمين — شاركه بشكل خاص».
