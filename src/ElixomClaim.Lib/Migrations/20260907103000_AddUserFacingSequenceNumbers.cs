using ElixomClaim.Lib.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElixomClaim.Lib.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260907103000_AddUserFacingSequenceNumbers")]
public partial class AddUserFacingSequenceNumbers : Migration
{
    private static readonly (string Table, string Sequence)[] Targets =
    [
        ("Claims", "ClaimSequenceNo"),
        ("ClaimComments", "ClaimCommentSequenceNo"),
        ("JobPayments", "JobPaymentSequenceNo"),
        ("CollectionClients", "CollectionClientSequenceNo"),
        ("CollectionTransactions", "CollectionTransactionSequenceNo"),
        ("SalaryDefinitions", "SalaryDefinitionSequenceNo"),
        ("SalaryAdjustments", "SalaryAdjustmentSequenceNo"),
        ("Payrolls", "PayrollSequenceNo"),
        ("PayrollEntries", "PayrollEntrySequenceNo")
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var (_, sequence) in Targets)
        {
            migrationBuilder.CreateSequence<long>(name: sequence, schema: "dbclaim");
        }

        foreach (var (table, sequence) in Targets)
        {
            migrationBuilder.AddColumn<long>(
                name: "SequenceNo",
                schema: "dbclaim",
                table: table,
                type: "bigint",
                nullable: false,
                defaultValueSql: $"NEXT VALUE FOR [dbclaim].[{sequence}]");

            migrationBuilder.CreateIndex(
                name: $"IX_{table}_SequenceNo",
                schema: "dbclaim",
                table: table,
                column: "SequenceNo",
                unique: true);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var (table, _) in Targets)
        {
            migrationBuilder.DropIndex(name: $"IX_{table}_SequenceNo", schema: "dbclaim", table: table);
            migrationBuilder.DropColumn(name: "SequenceNo", schema: "dbclaim", table: table);
        }

        foreach (var (_, sequence) in Targets)
        {
            migrationBuilder.DropSequence(name: sequence, schema: "dbclaim");
        }
    }
}
