using ElixomClaim.Lib.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElixomClaim.Lib.Migrations;

/// <inheritdoc />
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260903090000_AddAuditRecordAppendOnlyTrigger")]
public partial class AddAuditRecordAppendOnlyTrigger : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TRIGGER [dbclaim].[TR_AuditRecords_PreventMutation]
            ON [dbclaim].[AuditRecords]
            AFTER UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 51000, 'Audit records are append-only.', 1;
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER [dbclaim].[TR_AuditRecords_PreventMutation];");
    }
}
