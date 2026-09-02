# Architecture Decision Records

Architecture Decision Records (ADRs) preserve the reasoning behind decisions that are costly, risky, or difficult to reverse. They complement the short current-state ledger in `MEMORY.md`.

## When to write one

Create an ADR before implementing a decision involving security, OAuth, data retention, persistence model, financial accounting, external integration, deployment, or a significant change in project structure.

## Format

Use sequential filenames: `NNNN-short-title.md`. Include:

1. Status and date
2. Context/problem
3. Decision
4. Consequences and mitigations
5. Verification/review required
6. Links to superseded or related ADRs

Never include credentials, tokens, personal data, connection strings, or customer financial information.
