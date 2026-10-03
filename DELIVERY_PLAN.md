# Delivery Plan

## Phase 0 - Complete; awaiting owner approval
- Architecture and risk review and documentation
- ADRs for tenancy, CP-SAT, offline sync/printing, worker locking, deterministic solver mode, and RTL PDF
- C# Google.OrTools CP-SAT spike and QuestPDF/Noto Naskh Arabic PDF visual spike
- Git commit and `phase-0` tag are the final Phase 0 persistence gate

## Phase 1
- Solution skeleton
- CI/CD, Docker, PostgreSQL, Redis, auth, RBAC, tenancy and audit
- Architecture enforcement with tests

## Phase 2
- School Profile, teachers, subjects, stages, sections, shifts, bell system, academic calendar

## Phase 3
- Workload, resources, scheduling profiles, capacity analysis, validation

## Phase 4
- CP-SAT solver integration, worker process, diagnostics, cancellation, reproducibility

## Phase 5
- Timetable versioning, approval, publication, rollback, and manual editor

## Phase 6 - first setup task and Go/No-Go
1. Re-run the QuestPDF Arabic A4 landscape proof-of-render in the target build/container environment using the selected Cairo or Noto Naskh Arabic font. Inspect rendered PNGs for glyph joining, RTL column order, clipping, and page layout; record the baseline and automated visual regression.
2. **Phase 0 spike result:** GO for QuestPDF feasibility. .NET 9, QuestPDF 2026.9.1, and Noto Naskh Arabic rendered a one-page PDF; a PNG was inspected and Arabic joining, RTL table order, and layout were visible. This is a spike, not production acceptance.
3. **Phase 6 release gate:** production GO only after the target-container render and visual regression pass. If it fails, NO-GO for PDF release; open an ADR to assess server-side HTML-to-PDF using a pinned Chromium runtime as a fallback. Do not substitute client-side PDF generation.
- After the gate: PDF, Excel, print center, backup/restore, and legacy import

## Phase 7+
- Attendance, monitoring, dashboard, offline sync, analytics, performance, security hardening, and release validation

## Gate to proceed
Owner must respond with exact text: `approved`
