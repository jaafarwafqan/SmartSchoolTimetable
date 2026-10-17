# .sst test vectors

Shared by the desktop repository (`SstVectorTests`) and the phone app repository, which keeps an exact copy and checks it
against `SHA256SUMS`. The format is described in `docs/SST_PACKAGE_FORMAT.md`.

**Everything here is fictional.** The school, the names and the dates are invented. The three Ed25519 keys (school A, school A
recreated, school B) are TEST KEYS whose seeds are SHA-256 of public strings (`SstVectors.TestSeed`); they must never be used
for a real school.

| File | Expected (paired with school A, today = `vectors.json` → `today`) |
|---|---|
| `pairing.sst` | valid (self-signed; trusting it pairs the phone with school A) |
| `valid-teacher.sst` | valid; decoded content in `valid-teacher.expected.json`; `valid-teacher.qr.txt` is the same bytes as QR text |
| `valid-principal.sst` | valid; `valid-principal.expected.json` |
| `tampered.sst` | tampered (a lesson moved after signing) |
| `wrong-school.sst` | wrongSchool |
| `key-changed.sst` | keyChanged (school A, recreated key) |
| `expired.sst` | expired (year 2025-2026) |
| `future-format.sst` | newerFormat (payload format 2) |
| `not-paired.sst` | notPaired (read without any trusted school) |

The expected JSON files use: byte strings as base64, the school id as a UUID string, dates as `yyyy-MM-dd`, instants as
ISO 8601 with offset, the kind as `pairing` / `teacher` / `principal`.

Regenerate (only after a deliberate format change, then copy the whole folder to the phone repository):

```
$env:WRITE_SST_VECTORS='1'; dotnet test tests/SmartSchoolTimetable.Tests --filter "FullyQualifiedName~SstVectorTests"
```
