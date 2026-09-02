using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElixomClaim.Lib.Migrations
{
    /// <inheritdoc />
    public partial class AddJobPaymentEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "Payrolls",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payrolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payrolls_Users_UserId",
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
                name: "IX_Payrolls_UserId_Status",
                schema: "dbclaim",
                table: "Payrolls",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "JobPayments",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "Payrolls",
                schema: "dbclaim");
        }
    }
}
