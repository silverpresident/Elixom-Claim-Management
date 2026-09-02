# Sprint 03 — Payment Clearing House

## Ordered backlog

1. Add Collection Client, client users, bank details, client-scoped purpose/amount option entities, collection transaction, method/status enums, and indexes/constraints.
2. Build Admin client configuration and assigned-user management, subject to the confirmed ownership decision.
3. Implement the shared collection service: validate chosen client options, record teller/UTC payment date, capture transaction fee, and create receipt outbox/audit records in one transaction.
4. Implement durable outbox processing, SMTP/ACS senders, EmailLog persistence, retry/backoff, and deduplication. Use a development fake sender in tests.
5. Build Teller’s last-24-hour workspace, collection form, review/detail, receipt reissue, and clean HTML print view. Send only to valid configured/entered recipients.
6. Test client isolation, option validation, recipient construction, fee capture, retry/idempotency, receipt privacy, and print rendering semantics.

## Done when

- A teller can record a valid collection and obtain a print-ready HTML receipt; the durable notification path is observable and cannot silently duplicate a receipt.

## Progress

| Item | Status | Updated | Scope, evidence, or blocker |
| --- | --- | --- | --- |
| 1 | Not started | — | — |
| 2 | Not started | — | — |
| 3 | Not started | — | — |
| 4 | Not started | — | — |
| 5 | Not started | — | — |
| 6 | Not started | — | — |
