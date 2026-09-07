using ElixomClaim.Lib.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElixomClaim.Lib.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260907093000_AllowCustomCollectionTransactionValues")]
public partial class AllowCustomCollectionTransactionValues : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_CollectionTransactions_CollectionPurposeOptions_PurposeOptionId_CollectionClientId", schema: "dbclaim", table: "CollectionTransactions");
        migrationBuilder.DropForeignKey(name: "FK_CollectionTransactions_CollectionAmountOptions_AmountOptionId_CollectionClientId", schema: "dbclaim", table: "CollectionTransactions");

        migrationBuilder.AlterColumn<Guid>(name: "PurposeOptionId", schema: "dbclaim", table: "CollectionTransactions", type: "uniqueidentifier", nullable: true, oldClrType: typeof(Guid), oldType: "uniqueidentifier");
        migrationBuilder.AlterColumn<Guid>(name: "AmountOptionId", schema: "dbclaim", table: "CollectionTransactions", type: "uniqueidentifier", nullable: true, oldClrType: typeof(Guid), oldType: "uniqueidentifier");
        migrationBuilder.AddColumn<string>(name: "Purpose", schema: "dbclaim", table: "CollectionTransactions", type: "nvarchar(200)", maxLength: 200, nullable: true);
        migrationBuilder.Sql("UPDATE ct SET Purpose = po.Name FROM dbclaim.CollectionTransactions AS ct INNER JOIN dbclaim.CollectionPurposeOptions AS po ON po.Id = ct.PurposeOptionId AND po.CollectionClientId = ct.CollectionClientId;");
        migrationBuilder.AlterColumn<string>(name: "Purpose", schema: "dbclaim", table: "CollectionTransactions", type: "nvarchar(200)", maxLength: 200, nullable: false, oldClrType: typeof(string), oldType: "nvarchar(200)", oldNullable: true);

        migrationBuilder.AddForeignKey(name: "FK_CollectionTransactions_CollectionPurposeOptions_PurposeOptionId_CollectionClientId", schema: "dbclaim", table: "CollectionTransactions", columns: new[] { "PurposeOptionId", "CollectionClientId" }, principalSchema: "dbclaim", principalTable: "CollectionPurposeOptions", principalColumns: new[] { "Id", "CollectionClientId" }, onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_CollectionTransactions_CollectionAmountOptions_AmountOptionId_CollectionClientId", schema: "dbclaim", table: "CollectionTransactions", columns: new[] { "AmountOptionId", "CollectionClientId" }, principalSchema: "dbclaim", principalTable: "CollectionAmountOptions", principalColumns: new[] { "Id", "CollectionClientId" }, onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("Custom collection transactions cannot be safely downgraded because they do not have configured option records.");
}
