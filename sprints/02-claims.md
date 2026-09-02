# Sprint 02 — Claims

## Ordered backlog

1. Add `Claim` and append-only `ClaimComment` schema, global soft-delete query filter, lifecycle/payment status enums, indexes, and concurrency token.
2. Implement shared claim service commands: create draft, edit, submit, soft-delete, accept, reject, and add public/private comment; enforce actor and transition rules inside the service.
3. Build the User dashboard: own claims, clear status/payment state, add/edit/delete affordances only when valid, payment-history section, and accessible empty states.
4. Build Manager claim queue with Submitted default, status filter, detail timeline, comment controls, and accept/reject actions.
5. Audit all mutations and write unit/integration tests for ownership, soft deletion, comments visibility, invalid transitions, and concurrent update behavior.

## Done when

- A claimant can complete the draft-to-submission journey and management can decide it; private notes never leak to claimants.

## Progress

| Item | Status | Updated | Scope, evidence, or blocker |
| --- | --- | --- | --- |
| 1 | Complete | 2026-09-02 | ClaimStatus, ClaimPaymentStatus, Claim and ClaimComment entities, EF Core mappings in dbclaim schema with global soft-delete filters, AddClaimEntities migration. |
| 2 | Complete | 2026-09-02 | IClaimService and ClaimService with create, edit, submit, accept, reject, soft delete, comments, and audit logging. Registered in DI. |
| 3 | Complete | 2026-09-02 | ClaimsController and Views/Claims/ (Index, Create, Edit, Details) dashboard UI. |
| 4 | Complete | 2026-09-02 | ManagerClaimsController and Views/ManagerClaims/ (Index, Details) queue UI with status filters and private comment controls. |
| 5 | Complete | 2026-09-02 | ClaimServiceTests covering draft lifecycle, state transitions, soft deletion, and private comment visibility. Command: `dotnet test ElixomClaim.slnx` passed (81 tests total). |
