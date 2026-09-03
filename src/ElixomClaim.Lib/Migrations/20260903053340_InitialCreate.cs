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
            migrationBuilder.CreateTable(
                name: "EmailLogs",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OutboxItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    HtmlBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RelatedEntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                    Recipient = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
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
                name: "JobPayments",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayeeUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CollectionClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PublicNote = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    InternalNote = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
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
                    OriginalJobPaymentId = table.Column<long>(type: "bigint", nullable: true),
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
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
                name: "JobPaymentClaims",
                schema: "dbclaim",
                columns: table => new
                {
                    JobPaymentId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimId = table.Column<long>(type: "bigint", nullable: false)
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
                name: "JobPaymentCollections",
                schema: "dbclaim",
                columns: table => new
                {
                    JobPaymentId = table.Column<long>(type: "bigint", nullable: false),
                    CollectionTransactionId = table.Column<long>(type: "bigint", nullable: false)
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
                name: "JobPaymentDeductions",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobPaymentId = table.Column<long>(type: "bigint", nullable: false),
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalaryDefinitionId = table.Column<long>(type: "bigint", nullable: false),
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalaryDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PercentageRate = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    FixedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
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
                name: "JobPaymentPayrolls",
                schema: "dbclaim",
                columns: table => new
                {
                    JobPaymentId = table.Column<long>(type: "bigint", nullable: false),
                    PayrollId = table.Column<long>(type: "bigint", nullable: false)
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollId = table.Column<long>(type: "bigint", nullable: false),
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
                name: "IX_JobPayments_Status_ScheduledAtUtc",
                schema: "dbclaim",
                table: "JobPayments",
                columns: new[] { "Status", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_PayrollId_SortOrder",
                schema: "dbclaim",
                table: "PayrollEntries",
                columns: new[] { "PayrollId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payrolls_SalaryDefinitionId_PeriodEndingDate",
                schema: "dbclaim",
                table: "Payrolls",
                columns: new[] { "SalaryDefinitionId", "PeriodEndingDate" },
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
                name: "IX_SalaryDefinitions_UserId_IsActive",
                schema: "dbclaim",
                table: "SalaryDefinitions",
                columns: new[] { "UserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "PayrollEntries",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "SalaryAdjustments",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "JobPayments",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "Payrolls",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "SalaryDefinitions",
                schema: "dbclaim");
        }
    }
}
