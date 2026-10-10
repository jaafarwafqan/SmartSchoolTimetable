# ADR 0045: «أولويات الجدول» — three plain levels over the soft-rule weights

- Status: Accepted (MF9, branch `work/phase-5`)
- Date: 2026-10-10

## Context
The scheduling profile (Phase 3 §2.4) asked the owner to switch five soft rules on or off and pick a weight from 0 to 100. A number from 0 to 100 means nothing to a school owner, and the screen was named «ملف الجدولة», a developer word.

## Decisions
1. **The screen is «أولويات الجدول», under Settings › «متقدم»** (`/settings/advanced`; the old `/settings/scheduling` redirects). Each rule has a plain Arabic sentence saying what it tries to do, and a three-way choice: «غير مهم» · «مهم» · «مهم جداً». Hard constraints are never listed; the screen says they are always respected.
2. **The server model does not change.** Weights stay 0–100 per rule with an enabled flag, `ProfileVersion` still grows on each change, and the input hash and solver are untouched. The mapping lives in the frontend (`features/scheduling-profile/profileApi.ts`), tested by unit tests:

   | Level | Saved as |
   |---|---|
   | غير مهم | rule off (`enabled = false`, weight 0) |
   | مهم | on, weight 20 |
   | مهم جداً | on, weight 40 |

3. **Reading a saved rule** shows the nearest level: off or weight below 10 → «غير مهم»; 30 and above → «مهم جداً»; otherwise «مهم». The defaults (spread 20, gaps 30, heavy 15, repeated 25, doubles 10) therefore read as: gaps «مهم جداً», the other four «مهم».
4. **A rule is rewritten only when the owner changes its level.** Saving after changing one rule leaves the others' exact weights (and the "defaults applied" state of the untouched ones) as they were; «استعادة الإعدادات الافتراضية» still restores the exact default weights.

## Consequences
- No migration and no new error code. Audit events keep their names; their Arabic sentences now say «أولويات الجدول».
- Weights outside the three levels (set through the API) are still valid and display as the nearest level.
- Changing the mapping later is a frontend-only change; an owner who wants finer control would need a new decision (this reverses part of DECISIONS #55: weights in steps of five).
