using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElixomClaim.Lib.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollectionClients",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CollectionAmountOptions",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BranchCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                name: "CollectionPurposeOptions",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
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
                name: "CollectionTransactions",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurposeOptionId = table.Column<long>(type: "bigint", nullable: false),
                    AmountOptionId = table.Column<long>(type: "bigint", nullable: false),
                    TellerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PayorEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
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
                name: "IX_CollectionTransactions_TellerUserId_CreatedAtUtc",
                schema: "dbclaim",
                table: "CollectionTransactions",
                columns: new[] { "TellerUserId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectionClientBankDetails",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "CollectionClientUsers",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "CollectionTransactions",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "CollectionAmountOptions",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "CollectionPurposeOptions",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "CollectionClients",
                schema: "dbclaim");
        }
    }
}
