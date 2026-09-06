using ElixomClaim.Lib.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElixomClaim.Lib.Migrations;

/// <inheritdoc />
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260903120000_DomainDataCompletion")]
public partial class DomainDataCompletion : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BankAccountName",
            schema: "dbclaim",
            table: "Users",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BankName",
            schema: "dbclaim",
            table: "Users",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAtUtc",
            schema: "dbclaim",
            table: "CollectionPurposeOptions",
            type: "datetime2",
            nullable: false,
            defaultValueSql: "GETUTDATE()");

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAtUtc",
            schema: "dbclaim",
            table: "CollectionAmountOptions",
            type: "datetime2",
            nullable: false,
            defaultValueSql: "GETUTDATE()");

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAtUtc",
            schema: "dbclaim",
            table: "SalaryAdjustments",
            type: "datetime2",
            nullable: false,
            defaultValueSql: "GETUTDATE()");

        migrationBuilder.AddColumn<DateTime>(
            name: "SentAtUtc",
            schema: "dbclaim",
            table: "EmailLogs",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "DateOfJob",
            schema: "dbclaim",
            table: "Claims",
            type: "datetime2",
            nullable: false,
            defaultValueSql: "GETUTDATE()");

        migrationBuilder.AddColumn<DateTime>(
            name: "DeletedAtUtc",
            schema: "dbclaim",
            table: "Claims",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            schema: "dbclaim",
            table: "CollectionClients",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Notes",
            schema: "dbclaim",
            table: "CollectionClients",
            type: "nvarchar(4000)",
            maxLength: 4000,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "PerJobProcessingFee",
            schema: "dbclaim",
            table: "CollectionClients",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "PerTransactionFee",
            schema: "dbclaim",
            table: "CollectionClients",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<string>(
            name: "Notes",
            schema: "dbclaim",
            table: "CollectionClientBankDetails",
            type: "nvarchar(4000)",
            maxLength: 4000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PayorTelephone",
            schema: "dbclaim",
            table: "CollectionTransactions",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Title",
            schema: "dbclaim",
            table: "JobPayments",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PayoutBankName",
            schema: "dbclaim",
            table: "JobPayments",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PayoutBankAccountName",
            schema: "dbclaim",
            table: "JobPayments",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PayoutBankAccountNumber",
            schema: "dbclaim",
            table: "JobPayments",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PayoutBankBranchCode",
            schema: "dbclaim",
            table: "JobPayments",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "BankAccountName", schema: "dbclaim", table: "Users");
        migrationBuilder.DropColumn(name: "BankName", schema: "dbclaim", table: "Users");
        migrationBuilder.DropColumn(name: "CreatedAtUtc", schema: "dbclaim", table: "CollectionPurposeOptions");
        migrationBuilder.DropColumn(name: "CreatedAtUtc", schema: "dbclaim", table: "CollectionAmountOptions");
        migrationBuilder.DropColumn(name: "CreatedAtUtc", schema: "dbclaim", table: "SalaryAdjustments");
        migrationBuilder.DropColumn(name: "SentAtUtc", schema: "dbclaim", table: "EmailLogs");
        migrationBuilder.DropColumn(name: "DateOfJob", schema: "dbclaim", table: "Claims");
        migrationBuilder.DropColumn(name: "DeletedAtUtc", schema: "dbclaim", table: "Claims");
        migrationBuilder.DropColumn(name: "Description", schema: "dbclaim", table: "CollectionClients");
        migrationBuilder.DropColumn(name: "Notes", schema: "dbclaim", table: "CollectionClients");
        migrationBuilder.DropColumn(name: "PerJobProcessingFee", schema: "dbclaim", table: "CollectionClients");
        migrationBuilder.DropColumn(name: "PerTransactionFee", schema: "dbclaim", table: "CollectionClients");
        migrationBuilder.DropColumn(name: "Notes", schema: "dbclaim", table: "CollectionClientBankDetails");
        migrationBuilder.DropColumn(name: "PayorTelephone", schema: "dbclaim", table: "CollectionTransactions");
        migrationBuilder.DropColumn(name: "Title", schema: "dbclaim", table: "JobPayments");
        migrationBuilder.DropColumn(name: "PayoutBankName", schema: "dbclaim", table: "JobPayments");
        migrationBuilder.DropColumn(name: "PayoutBankAccountName", schema: "dbclaim", table: "JobPayments");
        migrationBuilder.DropColumn(name: "PayoutBankAccountNumber", schema: "dbclaim", table: "JobPayments");
        migrationBuilder.DropColumn(name: "PayoutBankBranchCode", schema: "dbclaim", table: "JobPayments");
    }
}
