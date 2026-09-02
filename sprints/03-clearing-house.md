# Sprint 03 — Payment Clearing House

## Ordered backlog

1. Add Collection Client, client users, bank details, client-scoped purpose/amount option entities, collection transaction, method/status enums, and indexes/constraints.
2. Build Administrator-only client configuration and assigned-user management; do not expose client options, bank details, or assignments for client-user self-service editing.
3. Implement the shared collection service: validate chosen client options, record teller/UTC payment date, capture transaction fee, and create receipt outbox/audit records in one transaction.
4. Implement durable outbox processing, SMTP/ACS senders, EmailLog persistence, retry/backoff, deduplication, recipient validation, and a recorded `SkippedInvalidRecipient` outcome. A missing/invalid optional payor email must not block client/system-copy delivery. Use a development fake sender in tests.
5. Build Teller’s last-24-hour workspace, collection form, review/detail, receipt reissue, and clean HTML print view. Send only to valid configured/entered recipients.
6. Test client isolation, option validation, recipient construction/skipping, fee capture, retry/idempotency, receipt privacy, retention settings, and print rendering semantics.

## Done when

- A teller can record a valid collection and obtain a print-ready HTML receipt; the durable notification path is observable and cannot silently duplicate a receipt.

## Progress

| Item | Status | Updated | Scope, evidence, or blocker |
| --- | --- | --- | --- |
| 1 | Complete | 2026-09-02 | Added collection client, assignment, bank-detail, client-scoped purpose/amount option, and collection transaction entities with composite client-option foreign keys, indexes, `decimal(18,2)` JMD values, and lifecycle enums. Migration: `20260902214419_AddCollectionEntities`. Tests: `CollectionModelTests`; `dotnet test src/ElixomClaim.Lib.Tests/ElixomClaim.Lib.Tests.csproj --no-restore` passed (62). |
| 2 | Complete | 2026-09-02 | Added shared administrator-only client configuration service and `/admin/collection-clients` MVC configuration routes/views for clients, assignments, options, and bank details. Service independently checks active Administrator role and audits mutations; no client-user self-service routes exist. Tests: `CollectionClientAdministrationServiceTests`; `dotnet test src/ElixomClaim.Lib.Tests/ElixomClaim.Lib.Tests.csproj --no-restore` passed (64); `dotnet build src/ElixomClaim.Web/ElixomClaim.Web.csproj --no-restore` passed. |
| 3 | Complete | 2026-09-02 | Added `CollectionService.RecordAsync`: shared active-teller authorization, client-scoped active option validation, fee/date capture, and relational transaction covering collection, receipt outbox, and audit persistence. Added durable `EmailOutboxItems` schema in migration `20260902214751_AddEmailOutbox`. Tests: `CollectionServiceTests`; `dotnet test src/ElixomClaim.Lib.Tests/ElixomClaim.Lib.Tests.csproj --no-restore` passed (66). |
| 4 | Complete | 2026-09-02 | Added durable dispatcher, `EmailLogs`, retry/backoff (five attempts), idempotent outbox keys, recipient validation, SMTP/ACS/fake sender adapters, and hosted dispatcher. Missing/invalid payor recipients are persisted as `SkippedInvalidRecipient` without blocking client/system copies. Migration: `20260902215402_AddEmailLogs`. Tests: `OutboxServiceTests`; `dotnet test src/ElixomClaim.Lib.Tests/ElixomClaim.Lib.Tests.csproj --no-restore` passed (69), Web tests passed (24), solution build passed. |
| 5 | Complete | 2026-09-02 | Added `/collections` Teller workspace (last 24 hours), record form, detail/review, controlled receipt reissue, and responsive HTML print route. Reissue is limited to the recording teller or Manager+ and only uses previously valid configured recipients; receipt omits internal processing fee. `dotnet build ElixomClaim.slnx --no-restore` and `dotnet test ElixomClaim.slnx --no-restore` passed (69 Lib + 24 Web tests). |
| 6 | Complete | 2026-09-02 | Added focused tests for cross-client option rejection, fee capture, invalid/missing payor skip + logged system delivery, retry/idempotency, email/print fee privacy, print HTML semantics, and four-year retention floor. `dotnet build ElixomClaim.slnx --no-restore` and `dotnet test ElixomClaim.slnx --no-restore` passed (72 Lib + 25 Web tests). |
