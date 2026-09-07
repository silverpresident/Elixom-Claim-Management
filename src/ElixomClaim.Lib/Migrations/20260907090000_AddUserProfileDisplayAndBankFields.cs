using ElixomClaim.Lib.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElixomClaim.Lib.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260907090000_AddUserProfileDisplayAndBankFields")]
public partial class AddUserProfileDisplayAndBankFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "DisplayName", schema: "dbclaim", table: "Users", type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "BankBranchName", schema: "dbclaim", table: "Users", type: "nvarchar(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<string>(name: "BankAccountType", schema: "dbclaim", table: "Users", type: "nvarchar(50)", maxLength: 50, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DisplayName", schema: "dbclaim", table: "Users");
        migrationBuilder.DropColumn(name: "BankBranchName", schema: "dbclaim", table: "Users");
        migrationBuilder.DropColumn(name: "BankAccountType", schema: "dbclaim", table: "Users");
    }
}
