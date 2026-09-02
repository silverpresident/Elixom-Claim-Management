# Sprint 00 — Foundation

## Goal

Create a reliable, attractive base that later business work can extend without structural rework.

## Ordered backlog

1. Create the solution and four projects under `src/`; remove every `Class1.cs`; add central package/version management if adopted.
2. Add the Lib DI extension, Azure SQL `DbContext`, `dbclaim` default schema, entity base conventions, UTC/time abstraction, decimal conventions, health checks, and guarded migration/startup-seed extension.
3. Establish migrations workflow: local developer database guidance, production single-runner policy, migration test, and no automatic destructive schema action.
4. Create the MVC layout, responsive navigation, status-badge component, validation summary, accessible empty/error states, footer, SVG favicon, and print stylesheet. Reference Bootstrap 5.3 and jQuery 3.7 via CDN with SRI where available.
5. Add a meaningful privacy page with clearly marked legal-review placeholders for contact, retention, and jurisdiction.
6. Introduce typed/redacted configuration options and validation for database, Google, email, and OAuth settings; document secrets setup.
7. Configure structured `ILogger<T>` logging, correlation IDs, error handling, health/readiness endpoints, and baseline test infrastructure.

## Done when

- The solution builds/tests cleanly, renders the shell on desktop/mobile, and has no local Bootstrap/jQuery or PDF dependency.
- A migration creates only `dbclaim` objects and configuration failures are understandable without leaking secrets.

## Progress

| Item | Status | Updated | Scope, evidence, or blocker |
| --- | --- | --- | --- |
| 1 | Not started | — | — |
| 2 | Not started | — | — |
| 3 | Not started | — | — |
| 4 | Not started | — | — |
| 5 | Not started | — | — |
| 6 | Not started | — | — |
| 7 | Not started | — | — |
