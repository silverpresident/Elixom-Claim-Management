using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElixomClaim.Lib.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbclaim");

            migrationBuilder.CreateSequence(
                name: "ClaimCommentSequenceNo",
                schema: "dbclaim");

            migrationBuilder.CreateSequence(
                name: "ClaimSequenceNo",
                schema: "dbclaim");

            migrationBuilder.CreateSequence(
                name: "CollectionClientSequenceNo",
                schema: "dbclaim");

            migrationBuilder.CreateSequence(
                name: "CollectionTransactionSequenceNo",
                schema: "dbclaim");

            migrationBuilder.CreateSequence(
                name: "JobPaymentSequenceNo",
                schema: "dbclaim");

            migrationBuilder.CreateSequence(
                name: "PayrollEntrySequenceNo",
                schema: "dbclaim");

            migrationBuilder.CreateSequence(
                name: "PayrollSequenceNo",
                schema: "dbclaim");

            migrationBuilder.CreateSequence(
                name: "SalaryAdjustmentSequenceNo",
                schema: "dbclaim");

            migrationBuilder.CreateSequence(
                name: "SalaryDefinitionSequenceNo",
                schema: "dbclaim");

            migrationBuilder.CreateTable(
                name: "AuditRecords",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ActorEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BeforeStateJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterStateJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsMcpOperation = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CollectionClients",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR [dbclaim].[CollectionClientSequenceNo]"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PerJobProcessingFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PerTransactionFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailLogs",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OutboxItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    To = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    From = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Cc = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Bcc = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    HtmlBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RelatedEntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailOutboxItems",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    To = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    From = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Cc = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Bcc = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    HtmlBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RelatedEntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    AvailableAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOutboxItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OAuthAuthorizationCodes",
                schema: "dbclaim",
                columns: table => new
                {
                    CodeHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RedirectUri = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CodeChallenge = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CodeChallengeMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthAuthorizationCodes", x => x.CodeHash);
                });

            migrationBuilder.CreateTable(
                name: "OAuthClients",
                schema: "dbclaim",
                columns: table => new
                {
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientSecretHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ClientType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Confidential"),
                    RedirectUrisJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AllowedGrantTypes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AllowedScopes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthClients", x => x.ClientId);
                });

            migrationBuilder.CreateTable(
                name: "OAuthConsents",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthConsents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OAuthTokens",
                schema: "dbclaim",
                columns: table => new
                {
                    TokenHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TokenId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TokenType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RefreshTokenFamilyId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthTokens", x => x.TokenHash);
                });

            migrationBuilder.CreateTable(
                name: "OperationRecords",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessingStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    BankAccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BankBranchCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BankBranchName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BankAccountType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CollectionAmountOptions",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionAmountOptions", x => x.Id);
                    table.UniqueConstraint("AK_CollectionAmountOptions_Id_CollectionClientId", x => new { x.Id, x.CollectionClientId });
                    table.ForeignKey(
                        name: "FK_CollectionAmountOptions_CollectionClients_CollectionClientId",
                        column: x => x.CollectionClientId,
                        principalSchema: "dbclaim",
                        principalTable: "CollectionClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionClientBankDetails",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BranchCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BranchName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionClientBankDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionClientBankDetails_CollectionClients_CollectionClientId",
                        column: x => x.CollectionClientId,
                        principalSchema: "dbclaim",
                        principalTable: "CollectionClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionPurposeOptions",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionPurposeOptions", x => x.Id);
                    table.UniqueConstraint("AK_CollectionPurposeOptions_Id_CollectionClientId", x => new { x.Id, x.CollectionClientId });
                    table.ForeignKey(
                        name: "FK_CollectionPurposeOptions_CollectionClients_CollectionClientId",
                        column: x => x.CollectionClientId,
                        principalSchema: "dbclaim",
                        principalTable: "CollectionClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Claims",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR [dbclaim].[ClaimSequenceNo]"),
                    ClaimantUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "JMD"),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaymentStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DateOfJob = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Claims_Users_ClaimantUserId",
                        column: x => x.ClaimantUserId,
                        principalSchema: "dbclaim",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionClientUsers",
                schema: "dbclaim",
                columns: table => new
                {
                    CollectionClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionClientUsers", x => new { x.CollectionClientId, x.UserId });
                    table.ForeignKey(
                        name: "FK_CollectionClientUsers_CollectionClients_CollectionClientId",
                        column: x => x.CollectionClientId,
                        principalSchema: "dbclaim",
                        principalTable: "CollectionClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionClientUsers_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbclaim",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobPayments",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR [dbclaim].[JobPaymentSequenceNo]"),
                    PayeeUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CollectionClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PublicNote = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    InternalNote = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PayoutBankName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PayoutBankAccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PayoutBankAccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PayoutBankBranchCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    JobTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ClientProcessingFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTxnProcessingFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPaid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "JMD"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentTransactionNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OriginalJobPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsAdjustment = table.Column<bool>(type: "bit", nullable: false),
                    IsRecoveryReceivable = table.Column<bool>(type: "bit", nullable: false),
                    AdjustmentReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPayments", x => x.Id);
                    table.CheckConstraint("CK_JobPayments_ExactlyOnePayee", "([PayeeUserId] IS NOT NULL AND [CollectionClientId] IS NULL) OR ([PayeeUserId] IS NULL AND [CollectionClientId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_JobPayments_CollectionClients_CollectionClientId",
                        column: x => x.CollectionClientId,
                        principalSchema: "dbclaim",
                        principalTable: "CollectionClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobPayments_JobPayments_OriginalJobPaymentId",
                        column: x => x.OriginalJobPaymentId,
                        principalSchema: "dbclaim",
                        principalTable: "JobPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobPayments_Users_PayeeUserId",
                        column: x => x.PayeeUserId,
                        principalSchema: "dbclaim",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalaryDefinitions",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR [dbclaim].[SalaryDefinitionSequenceNo]"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FirstSalaryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LastSalaryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RecurrenceDays = table.Column<int>(type: "int", nullable: false),
                    RecurrenceMonths = table.Column<int>(type: "int", nullable: false),
                    NearestWeekday = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryDefinitions", x => x.Id);
                    table.CheckConstraint("CK_SalaryDefinitions_BaseAmount", "[BaseAmount] > 0");
                    table.CheckConstraint("CK_SalaryDefinitions_DateRange", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
                    table.CheckConstraint("CK_SalaryDefinitions_NearestWeekday", "[NearestWeekday] >= 0 AND [NearestWeekday] <= 6");
                    table.CheckConstraint("CK_SalaryDefinitions_Recurrence", "[RecurrenceDays] >= 0 AND [RecurrenceMonths] >= 0 AND ([RecurrenceDays] > 0 OR [RecurrenceMonths] > 0)");
                    table.ForeignKey(
                        name: "FK_SalaryDefinitions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbclaim",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionTransactions",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR [dbclaim].[CollectionTransactionSequenceNo]"),
                    CollectionClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurposeOptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AmountOptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TellerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PayorEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PayorTelephone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Method = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProcessingFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "JMD"),
                    PaymentDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionTransactions_CollectionAmountOptions_AmountOptionId_CollectionClientId",
                        columns: x => new { x.AmountOptionId, x.CollectionClientId },
                        principalSchema: "dbclaim",
                        principalTable: "CollectionAmountOptions",
                        principalColumns: new[] { "Id", "CollectionClientId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionTransactions_CollectionClients_CollectionClientId",
                        column: x => x.CollectionClientId,
                        principalSchema: "dbclaim",
                        principalTable: "CollectionClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionTransactions_CollectionPurposeOptions_PurposeOptionId_CollectionClientId",
                        columns: x => new { x.PurposeOptionId, x.CollectionClientId },
                        principalSchema: "dbclaim",
                        principalTable: "CollectionPurposeOptions",
                        principalColumns: new[] { "Id", "CollectionClientId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionTransactions_Users_TellerUserId",
                        column: x => x.TellerUserId,
                        principalSchema: "dbclaim",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimComments",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR [dbclaim].[ClaimCommentSequenceNo]"),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsPrivate = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimComments_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalSchema: "dbclaim",
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClaimComments_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalSchema: "dbclaim",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobPaymentClaims",
                schema: "dbclaim",
                columns: table => new
                {
                    JobPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPaymentClaims", x => new { x.JobPaymentId, x.ClaimId });
                    table.ForeignKey(
                        name: "FK_JobPaymentClaims_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalSchema: "dbclaim",
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobPaymentClaims_JobPayments_JobPaymentId",
                        column: x => x.JobPaymentId,
                        principalSchema: "dbclaim",
                        principalTable: "JobPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobPaymentDeductions",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPaymentDeductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobPaymentDeductions_JobPayments_JobPaymentId",
                        column: x => x.JobPaymentId,
                        principalSchema: "dbclaim",
                        principalTable: "JobPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payrolls",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR [dbclaim].[PayrollSequenceNo]"),
                    SalaryDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodEndingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PayrollTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payrolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payrolls_SalaryDefinitions_SalaryDefinitionId",
                        column: x => x.SalaryDefinitionId,
                        principalSchema: "dbclaim",
                        principalTable: "SalaryDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payrolls_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbclaim",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalaryAdjustments",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR [dbclaim].[SalaryAdjustmentSequenceNo]"),
                    SalaryDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PercentageRate = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    FixedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryAdjustments", x => x.Id);
                    table.CheckConstraint("CK_SalaryAdjustments_Range", "[PercentageRate] >= 0 AND [PercentageRate] <= 1 AND [FixedValue] >= 0");
                    table.ForeignKey(
                        name: "FK_SalaryAdjustments_SalaryDefinitions_SalaryDefinitionId",
                        column: x => x.SalaryDefinitionId,
                        principalSchema: "dbclaim",
                        principalTable: "SalaryDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobPaymentCollections",
                schema: "dbclaim",
                columns: table => new
                {
                    JobPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPaymentCollections", x => new { x.JobPaymentId, x.CollectionTransactionId });
                    table.ForeignKey(
                        name: "FK_JobPaymentCollections_CollectionTransactions_CollectionTransactionId",
                        column: x => x.CollectionTransactionId,
                        principalSchema: "dbclaim",
                        principalTable: "CollectionTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobPaymentCollections_JobPayments_JobPaymentId",
                        column: x => x.JobPaymentId,
                        principalSchema: "dbclaim",
                        principalTable: "JobPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobPaymentPayrolls",
                schema: "dbclaim",
                columns: table => new
                {
                    JobPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPaymentPayrolls", x => new { x.JobPaymentId, x.PayrollId });
                    table.ForeignKey(
                        name: "FK_JobPaymentPayrolls_JobPayments_JobPaymentId",
                        column: x => x.JobPaymentId,
                        principalSchema: "dbclaim",
                        principalTable: "JobPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobPaymentPayrolls_Payrolls_PayrollId",
                        column: x => x.PayrollId,
                        principalSchema: "dbclaim",
                        principalTable: "Payrolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollEntries",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR [dbclaim].[PayrollEntrySequenceNo]"),
                    PayrollId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollEntries_Payrolls_PayrollId",
                        column: x => x.PayrollId,
                        principalSchema: "dbclaim",
                        principalTable: "Payrolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_EntityType_EntityId_OccurredAtUtc",
                schema: "dbclaim",
                table: "AuditRecords",
                columns: new[] { "EntityType", "EntityId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimComments_AuthorUserId",
                schema: "dbclaim",
                table: "ClaimComments",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimComments_ClaimId",
                schema: "dbclaim",
                table: "ClaimComments",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimComments_SequenceNo",
                schema: "dbclaim",
                table: "ClaimComments",
                column: "SequenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_ClaimantUserId",
                schema: "dbclaim",
                table: "Claims",
                column: "ClaimantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_PaymentStatus",
                schema: "dbclaim",
                table: "Claims",
                column: "PaymentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_SequenceNo",
                schema: "dbclaim",
                table: "Claims",
                column: "SequenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Status",
                schema: "dbclaim",
                table: "Claims",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionAmountOptions_CollectionClientId_Name",
                schema: "dbclaim",
                table: "CollectionAmountOptions",
                columns: new[] { "CollectionClientId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionClientBankDetails_CollectionClientId_IsActive",
                schema: "dbclaim",
                table: "CollectionClientBankDetails",
                columns: new[] { "CollectionClientId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionClients_Name",
                schema: "dbclaim",
                table: "CollectionClients",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionClients_SequenceNo",
                schema: "dbclaim",
                table: "CollectionClients",
                column: "SequenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionClientUsers_UserId",
                schema: "dbclaim",
                table: "CollectionClientUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPurposeOptions_CollectionClientId_Name",
                schema: "dbclaim",
                table: "CollectionPurposeOptions",
                columns: new[] { "CollectionClientId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTransactions_AmountOptionId_CollectionClientId",
                schema: "dbclaim",
                table: "CollectionTransactions",
                columns: new[] { "AmountOptionId", "CollectionClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTransactions_CollectionClientId_Status_PaymentDateUtc",
                schema: "dbclaim",
                table: "CollectionTransactions",
                columns: new[] { "CollectionClientId", "Status", "PaymentDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTransactions_PurposeOptionId_CollectionClientId",
                schema: "dbclaim",
                table: "CollectionTransactions",
                columns: new[] { "PurposeOptionId", "CollectionClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTransactions_SequenceNo",
                schema: "dbclaim",
                table: "CollectionTransactions",
                column: "SequenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTransactions_TellerUserId_CreatedAtUtc",
                schema: "dbclaim",
                table: "CollectionTransactions",
                columns: new[] { "TellerUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailLogs_CreatedAtUtc",
                schema: "dbclaim",
                table: "EmailLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EmailLogs_OutboxItemId",
                schema: "dbclaim",
                table: "EmailLogs",
                column: "OutboxItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutboxItems_IdempotencyKey",
                schema: "dbclaim",
                table: "EmailOutboxItems",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutboxItems_Status_AvailableAtUtc",
                schema: "dbclaim",
                table: "EmailOutboxItems",
                columns: new[] { "Status", "AvailableAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_JobPaymentClaims_ClaimId",
                schema: "dbclaim",
                table: "JobPaymentClaims",
                column: "ClaimId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobPaymentCollections_CollectionTransactionId",
                schema: "dbclaim",
                table: "JobPaymentCollections",
                column: "CollectionTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobPaymentDeductions_JobPaymentId",
                schema: "dbclaim",
                table: "JobPaymentDeductions",
                column: "JobPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPaymentPayrolls_PayrollId",
                schema: "dbclaim",
                table: "JobPaymentPayrolls",
                column: "PayrollId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobPayments_CollectionClientId",
                schema: "dbclaim",
                table: "JobPayments",
                column: "CollectionClientId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPayments_OriginalJobPaymentId",
                schema: "dbclaim",
                table: "JobPayments",
                column: "OriginalJobPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPayments_PayeeUserId",
                schema: "dbclaim",
                table: "JobPayments",
                column: "PayeeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPayments_SequenceNo",
                schema: "dbclaim",
                table: "JobPayments",
                column: "SequenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobPayments_Status_ScheduledAtUtc",
                schema: "dbclaim",
                table: "JobPayments",
                columns: new[] { "Status", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OAuthConsents_UserId_ClientId",
                schema: "dbclaim",
                table: "OAuthConsents",
                columns: new[] { "UserId", "ClientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationRecords_IdempotencyKey",
                schema: "dbclaim",
                table: "OperationRecords",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationRecords_OperationType_Status_ProcessingStartedAtUtc",
                schema: "dbclaim",
                table: "OperationRecords",
                columns: new[] { "OperationType", "Status", "ProcessingStartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_PayrollId_SortOrder",
                schema: "dbclaim",
                table: "PayrollEntries",
                columns: new[] { "PayrollId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_SequenceNo",
                schema: "dbclaim",
                table: "PayrollEntries",
                column: "SequenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payrolls_SalaryDefinitionId_PeriodEndingDate",
                schema: "dbclaim",
                table: "Payrolls",
                columns: new[] { "SalaryDefinitionId", "PeriodEndingDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payrolls_SequenceNo",
                schema: "dbclaim",
                table: "Payrolls",
                column: "SequenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payrolls_UserId_Status",
                schema: "dbclaim",
                table: "Payrolls",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryAdjustments_SalaryDefinitionId_Type",
                schema: "dbclaim",
                table: "SalaryAdjustments",
                columns: new[] { "SalaryDefinitionId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryAdjustments_SequenceNo",
                schema: "dbclaim",
                table: "SalaryAdjustments",
                column: "SequenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryDefinitions_SequenceNo",
                schema: "dbclaim",
                table: "SalaryDefinitions",
                column: "SequenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryDefinitions_UserId_IsActive",
                schema: "dbclaim",
                table: "SalaryDefinitions",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                schema: "dbclaim",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbclaim].[TR_AuditRecords_PreventMutation]
                ON [dbclaim].[AuditRecords]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, 'Audit records are append-only and cannot be modified or deleted.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER [dbclaim].[TR_AuditRecords_PreventMutation];");

            migrationBuilder.DropTable(
                name: "AuditRecords",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "ClaimComments",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "CollectionClientBankDetails",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "CollectionClientUsers",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "EmailLogs",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "EmailOutboxItems",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "JobPaymentClaims",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "JobPaymentCollections",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "JobPaymentDeductions",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "JobPaymentPayrolls",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "OAuthAuthorizationCodes",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "OAuthClients",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "OAuthConsents",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "OAuthTokens",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "OperationRecords",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "PayrollEntries",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "SalaryAdjustments",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "Claims",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "CollectionTransactions",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "JobPayments",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "Payrolls",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "CollectionAmountOptions",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "CollectionPurposeOptions",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "SalaryDefinitions",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "CollectionClients",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "dbclaim");

            migrationBuilder.DropSequence(
                name: "ClaimCommentSequenceNo",
                schema: "dbclaim");

            migrationBuilder.DropSequence(
                name: "ClaimSequenceNo",
                schema: "dbclaim");

            migrationBuilder.DropSequence(
                name: "CollectionClientSequenceNo",
                schema: "dbclaim");

            migrationBuilder.DropSequence(
                name: "CollectionTransactionSequenceNo",
                schema: "dbclaim");

            migrationBuilder.DropSequence(
                name: "JobPaymentSequenceNo",
                schema: "dbclaim");

            migrationBuilder.DropSequence(
                name: "PayrollEntrySequenceNo",
                schema: "dbclaim");

            migrationBuilder.DropSequence(
                name: "PayrollSequenceNo",
                schema: "dbclaim");

            migrationBuilder.DropSequence(
                name: "SalaryAdjustmentSequenceNo",
                schema: "dbclaim");

            migrationBuilder.DropSequence(
                name: "SalaryDefinitionSequenceNo",
                schema: "dbclaim");
        }
    }
}
