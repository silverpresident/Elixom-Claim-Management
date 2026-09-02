ELixom CLaim

Clean up this collection of ramblings into a proper technical spec for a c# mvc solution. Note that the bootstrap and jquery must not be served locally and must come from a cdn. 
also you should ask me clarifying questions before giving me the specs. 




You are a senior software developer tasked with reading a PHP project, reading documentation, and verifying its implementation as a C# .NET project.



Build 2 dotnet 10 c# MVC projects (and tests), place them all in a src folder.
1. A Lib project to act as the central library for all services, DI, entities, models, contexts and extensions.
2. A Web project to implement all the other features.
maintain compatibility but upgrade the project to suit

Use Azure SQL Server with a custom database schema name.

1. Write a proper privacy page.
2. Use bootstrap from a CDN and not a locally hosted version.
3. Generate and use a proper SVG icon for the favicon 
4. Remove the unnecessary Class1.cs
5. Add logging with ILogger to all controller, services and anywhere else it can be useful.

You must generate an AGENTS.md file with proper rules. Include instructions for a MEMORY.md system


The system has 4 functions
- Allow persons to make a claim
- Allow managers to review and manage users and claims
- Allow managers to review and manage payroll and salaries
- Allow managers cashier style payment (called the  payment clearing house)


User roles
- Blocked: default, cannot do anything on the system
- User: Can make claims (default role), manage own profile and mange bank information
- Teller: can access the payment clearing house feature
- Manager: can access the claims management and teller features
- Accountant: can access all features of manager and the payroll/salary feature
- Administrator: can manage users and do everything else


Project must have MCP with authorization, full Oauth2.0 setup

## CLAIM and DEFAULT DASHBOARD
This dashboard has a list of existing claims with statuses displayed and a button to add a claim.
The default user dashboard also shows payments made.

A claim can be deleted and edited if it not accepted
A claim has a title, description, date of job,  and total claimed
A claim also store the created at datetime
A claim has a status (draft, submitted, accepted, rejected)
A delete is a soft delete

## TELLER DASHBOARD
If the user has teller role they will have access to a dashboard which allows collecting payments and issuing receipts.
Their dashboard shows recent collections (last 24 hours) and allows review and reissuing of receipt.
To create a collection transaction the teller
- Select from a list of Collection Clients (payee)
- Enters a name (payor)
- Optional email of payor
- Optional telephone of payor
- Purpose of payment (has an associated html list value set in the payee profile)
- Amount collected (has an associated html list value set in the payee profile)
- Method of collection (cash, POS, bank transfer, credit note)
- Date of payment (default to current value)

When confirmed and saved
- record is saved in db
- notification is sent to payor
- notification is sent to payee
- notification is sent to an email address configure in the app settings for system copies

notification is the receipt
there should be a html printable version of the receipt that can be opened as well


## Manager Dashboard

### Review claims
- see all claims with status filter but by default only show submitted (unpaid)
- accept or reject a claim
- leave a comment on the claim (displayed to the claimant)
- leave a private comment (only visible to management)
- Add it to an existing job payment or use it to create a new job payment (only if accepted)
- when added to a job payment, claim's payment status goes to processing
- a job payment can have more than 1 Claims


### Review Collections
- see all collections filtered by client
- Add it to an existing job payment or use it to create a new job payment (only if accepted)
- a job payment can have more than 1 Collections but all must be from the same client
- collections should have a transfer status which starts at "Collected" and moves to "Processing" when added to a job payment, when the job payment is maid the status goes to "Transferred"
- a collection can only be added to a job payment if it is in the "Collected" status

### Review Job Payment
- see a list of job payments, status and details.
- for each item print the details or resend the notification.
- If it is still "pending" remove items from it.
- Add deduction items (description , amount)

### A Job Payment
along with other necessary properties it has
- Date Created and Date Paid
- A list of Claims
- A list of collections
- a list of deductions
- a list of payrolls (though it is usually just 1)
- A claimant or a Payee (the user claiming or the payee client)
- A title
- A description (sent to payee)
- An internal note (not sent to payee or displayed in printouts)
- Job Total
- Total Deductions
- Client Processing Fee
- Total Txn Processing Fee
- Total Paid (job total less processing fees less deductions)
- Date paid
- Paid to Name
- Paid to AccountNo
- Paid to Bank Name
- Paid to Branch
- Payment Transaction Number
- payment status  - Processing|Submitted|Scheduled|Paid  (only editable when processing)


Each collection item has a "internal processing fee" this is applied to clients who have a per transaction processing fee and then added up give the job's Total Txn Processing Fee

A client has among the relevant fields
- client name
- Notes
- per job processing fee
- per transaction fee
- 1 or more users assigned to the account (they can see the collections, job payments)
- Bank details
  - Account Name
  - bank AccountNo
  - Bank Name
  - bank Branch


The notification email (only sent when marked as paid) must have a well composed messages
- Client details
- payment details including totals, bank info date
- tabular list of claims and details (with total)
- tabular list of collections (with total)
- tabular list of deductions (with total)


When a job payment is recorded
- any payroll is updated to the "paid" status when the Job payment is recorded 
- any collection is updated to transferred
- any claim is updated to honoured


## Accountant Dashboard
In addition to everything else the role allows, only account can actually pay jobs.
For any job the account can mark it as scheduled so it is no longer editable.
Then when actually paid mark it a paid supplying the date and transaction number.
See a tabular list of accepted payments showing all the information need to enter it on the bank with a button to mark it scheduled
See a tabular list of scheduled payments showing all the information need to enter it on the bank with a button to mark it paid 
when mark paid the notification email is sent
- view jobs for payment
- view salary definitions
- view payroll

### Salary
A salary is a recurring payment.
You create a Salary definition.
Once created you either click generate now or you wait to the system cycle to generate it.

The definition has
- First salary date
- last salary date
- start date
- end date
- recurrence days
- recurrence months
- nearest day in month (stores a day of week)
- user (userid)
- description
- base amount
- a list of salary adjustment
- is_active

A salary adjustment has
- title
- percentage_rate (0.000 to 1.000)
- fixed_value
- type (deduction|benefit)

To get the due date of the next payment, take the last salary date, add the  recurrence months, then add recurrence days; adjust the date to the nearest day specified.
On the due date, generate a salary item and then set the last salary date
Note before generating a salary ensure it is after the start date and before the or on end date. 


## Payroll
a payroll item is a list of record that can only be create using a salary definition (cant be create manually)
Properties:
- perioding ending date
- user (userid)
- description
- Payroll total
- status (generated|submitted|paid)
- an ordered list of payroll entries

Payroll entries
- must be ordered, with the "Base" always being first followed by any salary benefit followed by and salary deductions, then other custom entires
- properties: description, amount (- or +), is_locked, a field for the ordering
When a payroll is generated the items from the  salary cannot be edited.
but custom entries can be added , these may be negative but not exceeding the remaining base after deductions, or positive
once submitted it cannot be edited and a Job payment is automatically created for the user.
the payroll is updated to the "paid" status when the Job payment is recorded 



Store all composed/sent email add associated details (to, from, cc, bcc, date etc) in a database table

Users must log in using Google SSO; appsettiings can specific a default email add as a fully admin user but users are added by adding them to a the table from teh admin interface with an email address, after that they can access with SSO

Remember the list of comments on a claim.

There should be proper audit logs kept and available to admin and managers.

MCP AI authenticates as a specific user, inherits their role, audit trail
SMTP and ACS
NO pdf
full custom OAuth2 authorization server built into the Web project for the MCP client