# ADR 0025: Default owner account (withdrawn)

- Status: **Withdrawn** on 2026-10-05 at the owner's instruction. Never re-introduce.
- Date: 2026-10-05

## What happened
- Commit `f827c5a` added a default owner account to make a fresh start easier:
  - `DefaultOwner` settings in `appsettings.json` (username `admin` with a fixed password);
  - `LocalAuthService.EnsureDefaultOwnerAsync`;
  - a start-up call in `Program.cs` that created the account on a database without an owner.
- It weakened the first-run design of ADR 0009: a public password, no recovery code shown, and no setup screen.

## Decision
- The change is reverted. An empty database has no account and shows the first-run setup screen: create the owner, confirm the recovery code, then the setup wizard (ADR 0023).
- `FirstRunTests` guard the rule:
  - an empty database reports `setupRequired` with zero owner rows;
  - the build fails if a default credential, a `DefaultOwner` setting, or a password value in `appsettings*.json` appears under `src/` or `frontend/src/`.
