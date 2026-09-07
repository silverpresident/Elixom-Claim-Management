using ElixomClaim.Lib.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElixomClaim.Lib.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260907100000_AddCollectionClientBankBranchAndAccountType")]
public partial class AddCollectionClientBankBranchAndAccountType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // A temporary empty default preserves legacy records during upgrade. New records
        // are rejected by the shared service unless both fields contain approved values.
        migrationBuilder.AddColumn<string>(
            name: "AccountType",
            schema: "dbclaim",
            table: "CollectionClientBankDetails",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "BranchName",
            schema: "dbclaim",
            table: "CollectionClientBankDetails",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AccountType", schema: "dbclaim", table: "CollectionClientBankDetails");
        migrationBuilder.DropColumn(name: "BranchName", schema: "dbclaim", table: "CollectionClientBankDetails");
    }
}
