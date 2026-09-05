using ElixomClaim.Lib.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElixomClaim.Lib.Migrations;

/// <inheritdoc />
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260903100000_AddOperationRecordsTable")]
public partial class AddOperationRecordsTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OperationRecords",
            schema: "dbclaim",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                IdempotencyKey = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                OperationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ExecutedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OperationRecords", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OperationRecords_IdempotencyKey",
            schema: "dbclaim",
            table: "OperationRecords",
            column: "IdempotencyKey",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OperationRecords",
            schema: "dbclaim");
    }
}
