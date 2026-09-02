# Engineering Guide for Agents

## First actions

1. Read `README.md`, then the relevant file in `sprints/`.
2. Read `MEMORY.md` before making an architectural decision.
3. Inspect the existing code and tests; do not overwrite unrelated user changes.
4. Implement the smallest coherent vertical slice, test it, and update `MEMORY.md` if the project’s durable state changed.

## Sprint execution and progress control

The `sprints/` directory is the source of truth for delivery order. Work only on the earliest sprint marked **Planned** or **In progress** in `MEMORY.md`; do not begin a later sprint merely because it is convenient.

1. Before editing code, read the current sprint in full and inspect its **Progress** section. Select one numbered backlog item only; do not claim an item already marked `In progress` or `Complete`.
2. Claim the item by adding/updating its progress row in the current sprint: item number, status, date, agent/branch identifier when known, concise scope, and affected files/areas. Use `In progress` only while active.
3. Keep the change set focused on the claimed item. If its implementation reveals prerequisite work, record it as `Blocked` with the reason and add a narrowly scoped prerequisite to the current sprint; do not silently implement later-sprint scope.
4. Before marking an item `Complete`, verify its acceptance behavior, run the relevant tests/build, record the commands and outcome in its progress row, and link the principal files, migration, and tests. A completed item must be usable by the next item without relying on unstated local work.
5. At every handoff, update the current sprint progress row and `MEMORY.md`: completed work, current active/blocked item, important decisions, schema/configuration changes, verification performed, and remaining risks. Never leave an abandoned `In progress` row.
6. A sprint advances only when every ordered item is `Complete`, its stated “Done when” checks pass, the sprint’s tests pass, and `MEMORY.md` changes its state to `Complete` and the next sprint to `In progress`. Record the completion date and evidence.

Avoid duplicate work by treating progress rows and the Git diff as the reservation record. If another agent’s ownership/status is unclear, inspect the current worktree and progress record first; coordinate or choose an unclaimed item instead of making overlapping edits. Do not change a `Complete` item without recording a regression/follow-up reason.

### Commit protocol

- Make one focused commit when a claimed backlog item is complete and its relevant verification has passed. Update the sprint Progress row and `MEMORY.md` before committing so the commit is self-describing and handoff-ready.
- Make an earlier checkpoint commit only when handing work to another agent, before a risky/destructive operation, or when the work is a coherent verified slice that will be continued later. Mark the item `In progress` and state the remaining work in its progress row.
- Do not commit unverified experiments, unrelated formatting, generated secrets, or another agent’s changes. Keep unrelated work out of the index and preserve a dirty worktree you do not own.
- Use imperative, scoped messages such as `feat(claims): add draft submission workflow` or `docs(sprint-00): record foundation completion`. Reference the sprint item in the commit body when useful.
- A schema migration, behavior change, and the tests that prove it belong in the same commit whenever practical. Never claim a sprint item is `Complete` merely because code was committed; its progress evidence must show the verification result.

## Definition of ready and decision records

Before claiming a backlog item, confirm it has a clear outcome, acceptance evidence, dependencies, affected security/data concerns, and a test approach. If any of these are unknown, mark it `Blocked` and ask for or record the missing decision rather than implementing a guess.

Use `adr/` for decisions that materially affect architecture, security, data governance, integrations, or delivery. Each ADR states the context, decision, consequences, date, and status. Add a new ADR rather than rewriting history; supersede an earlier ADR by linking both records. `MEMORY.md` records the current fact and links to the ADR.

## Non-negotiable architecture

- Target .NET 10, C# 14, ASP.NET Core MVC, EF Core, Azure SQL, and schema `dbclaim`.
- Keep business rules, entities, data access, and reusable services in `ElixomClaim.Lib`; keep HTTP/Razor/transport wiring in `ElixomClaim.Web`.
- Controllers, background workers, and MCP tools are thin adapters over shared services. Do not duplicate business decisions in an adapter.
- Split MCP tools into related files/classes (`ClaimTools`, `CollectionTools`, `JobPaymentTools`, `PayrollTools`, `EmailTools`, `OperationsTools`) with explicit DTO schemas. Never create a monolithic MCP tool class.
- MCP email tools may compose approved templates and queue authorized sends through the durable outbox; they must not enable arbitrary-recipient, free-form, bulk, or direct-provider email sending. MCP background-task tools request audited, idempotent domain operations and never invoke worker internals directly.
- Use async I/O, UTC timestamps, `decimal(18,2)` money, transactions for aggregate state changes, and database constraints for invariants that must survive concurrent requests.
- Remove default `Class1.cs` files. Do not introduce a local Bootstrap or jQuery distribution.

## Security and privacy

- Web sessions use Google OpenID Connect. Only provisioned active users may enter, except the explicitly configured bootstrap administrator flow.
- MCP uses the built-in OAuth authorization server with authorization code + PKCE S256. MCP actions inherit the concrete user identity and never bypass authorization.
- Treat bank details, emails, OAuth tokens, connection strings, and client secrets as sensitive. Never put them in source, tests, exception text, audit payloads, or application logs.
- Apply authorization and ownership checks in shared services, then enforce them at endpoints as defense in depth.
- Log security events, mutations, OAuth events, and MCP operations through the audit service. Keep audit records append-only.

## Domain guardrails

- Claims are soft-deleted and hidden by default. A claimant cannot change an accepted/rejected claim.
- A collection must be `Collected` before attachment, and every collection in a job has the same client.
- A job belongs to either a user or a collection client, never both; only `Processing` jobs can change line items.
- Marking a job paid performs all related status updates atomically and queues its notification exactly once.
- Payroll is generated only by salary definitions. Generated entries are locked; custom negative entries must not make net pay negative.
- Receipts and notifications are HTML only. PDF generation is prohibited.

## Frontend and quality

- Use Bootstrap and jQuery from CDN links only, with SRI where available. Prefer accessible, semantic Razor markup and responsive/print styles.
- Include `ILogger<T>` in controllers, services, and hosted services. Use structured, redacted logs.
- Add/maintain unit tests for services and lifecycle rules and integration tests for authorization, endpoints, and critical persistence behavior.
- Run the relevant formatter, build, and tests before handoff. Report commands run and limitations honestly.
- Use the `Result<T>` pattern

## MEMORY.md protocol

`MEMORY.md` is a concise ledger, not a task diary. Update it in the same change when any of these changes: architecture, schema/migration state, externally visible behavior, authorization/security posture, integrations/configuration contract, completed sprint, or a decision/risk needing future attention.

Each entry needs a date, concise fact/decision, affected area, and a link to the code, migration, test, issue, or sprint when available. Keep current facts in their named section and move superseded entries to the decision log; never paste secrets, token values, personal data, or verbose command output.
