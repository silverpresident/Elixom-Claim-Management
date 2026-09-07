
# Remaining Tasks

- [x] A regular user can set an optional display name on My Profile; their Google-provided full name remains unchanged.
- [x] Payout Bank Details include Branch Name and a required account-type selection (Savings or Current / Chequing).
- [x] The shared system display icon now uses an elegant plum gradient with a gold accent.
- [x] Authenticated navigation includes an anti-forgery-protected Sign out action.
- Add a SequenceNo field to the following entites and where a display term is needed use it instead of the GUID id: 
  - Claim
  - ClaimComment
  - JobPayment 
  - CollectionClient
  - CollectionTransaction
  - SalaryDefinition
  - SalaryAdjustment
  - Payroll
  - PayrollEntry
also update the various documentation, specs and notes to reflect this
- [x] CollectionClientBankDetail Bank Details include Branch Name and a required account-type selection (Savings or Current / Chequing); documentation, specifications, and project notes reflect this.
- Add mock data for CollectionTransaction when db is seeded and with each dev run.
- [x] Make this CollectionTransaction print out look more like a tradiaitona receipt and include the displayName of the teller. Display the payment date and time in local time.
- [x] Put payment method next to client. "Purpose" and  "Amount" needs to be input entry field with suggestion based on the list to allow other values to be entered. Payment Date/Time input needs to be local even if the UTC is stored.
- [x] Job printout now displays collection transactions, deductions, adjustment context, and linked payrolls with their entries. The general job-payment detail displays payroll details and links to its payroll workspace record; documentation, specifications, sprint evidence, and memory were updated.
