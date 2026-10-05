# ADR 0025: Default owner account on a new database

- Status: Accepted (owner decision, 2026-10-05). It amends the first-run setup in ADR 0009 and SECURITY.md.
- Date: 2026-10-05

## Context
The owner asked for a fixed admin account that is always there on a new database, so the app can be started fresh without the setup screen. Asked how fixed it should be, the owner chose: **created automatically, password changeable** (not hard-coded and unchangeable).

## Decision
- **Configuration:** `DefaultOwner` in `appsettings.json`: `Enabled` (true), `Username` (`admin`), `Password` (`Admin@12345`).
- **At start-up, after migrations:** if the database has **no** owner, `LocalAuthService.EnsureDefaultOwnerAsync` creates one with these credentials. It uses the same password hashing, rules and audit entry ("Default owner account created at first start.") as setup. An existing owner is never changed or recreated.
- **The app opens at the login screen**, not the setup screen. A warning is logged (without the password) to change it from Settings.
- **Recovery code:** one is generated and stored as a hash but never shown, and it is marked acknowledged. To have a usable recovery code, the owner generates one in Settings (this needs the current password).
- **Invalid configured credentials** (for example a short password) stop the start with an error instead of creating a weak account.
- **Tests and E2E** turn the default account off (`DefaultOwner:Enabled=false`), so the first-run setup flow stays covered. One test class covers the default account.

## Security consequences
- The default password is public: it is in the repository and in this ADR. Until it is changed, anyone who can use this Windows account and reach `127.0.0.1` can sign in. Remote access is still impossible: Kestrel is bound to loopback, and the Host/Origin checks and per-launch token are unchanged.
- **Mitigation:** the owner should change the password from Settings on first use. Setting `DefaultOwner:Enabled` to `false` restores the original setup screen.

## Alternatives considered
- **A hard-coded account that cannot be changed:** rejected by the owner's choice. It would also remove password change and recovery.
- **Default account in Development only:** the owner wanted it for the normal run.
