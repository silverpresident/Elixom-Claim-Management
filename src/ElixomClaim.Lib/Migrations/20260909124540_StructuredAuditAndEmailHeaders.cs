using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElixomClaim.Lib.Migrations
{
    /// <inheritdoc />
    public partial class StructuredAuditAndEmailHeaders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Retain legacy source columns for one release.  This is deliberately
            // additive: historical audit and delivery records are never rewritten.

            migrationBuilder.AddColumn<string>(
                name: "From",
                schema: "dbclaim",
                table: "EmailOutboxItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Bcc",
                schema: "dbclaim",
                table: "EmailOutboxItems",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "From",
                schema: "dbclaim",
                table: "EmailLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Cc",
                schema: "dbclaim",
                table: "EmailOutboxItems",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "To",
                schema: "dbclaim",
                table: "EmailOutboxItems",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurredAtUtc",
                schema: "dbclaim",
                table: "AuditRecords",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<string>(
                name: "Bcc",
                schema: "dbclaim",
                table: "EmailLogs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cc",
                schema: "dbclaim",
                table: "EmailLogs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "To",
                schema: "dbclaim",
                table: "EmailLogs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EntityId",
                schema: "dbclaim",
                table: "AuditRecords",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                schema: "dbclaim",
                table: "AuditRecords",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE [dbclaim].[AuditRecords]
                SET [EntityType] = LEFT([Target], CHARINDEX(':', [Target] + ':') - 1),
                    [EntityId] = CASE WHEN CHARINDEX(':', [Target]) > 0 THEN SUBSTRING([Target], CHARINDEX(':', [Target]) + 1, 100) ELSE [Target] END,
                    [OccurredAtUtc] = [TimestampUtc];
                UPDATE [dbclaim].[EmailOutboxItems] SET [To] = [Recipient];
                UPDATE [dbclaim].[EmailLogs] SET [To] = [Recipient];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_EntityType_EntityId_OccurredAtUtc",
                schema: "dbclaim",
                table: "AuditRecords",
                columns: new[] { "EntityType", "EntityId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditRecords_EntityType_EntityId_OccurredAtUtc",
                schema: "dbclaim",
                table: "AuditRecords");

            migrationBuilder.DropColumn(
                name: "Bcc",
                schema: "dbclaim",
                table: "EmailOutboxItems");

            migrationBuilder.DropColumn(
                name: "Cc",
                schema: "dbclaim",
                table: "EmailOutboxItems");

            migrationBuilder.DropColumn(
                name: "To",
                schema: "dbclaim",
                table: "EmailOutboxItems");

            migrationBuilder.DropColumn(
                name: "Bcc",
                schema: "dbclaim",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "Cc",
                schema: "dbclaim",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "To",
                schema: "dbclaim",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "EntityId",
                schema: "dbclaim",
                table: "AuditRecords");

            migrationBuilder.DropColumn(
                name: "EntityType",
                schema: "dbclaim",
                table: "AuditRecords");

            migrationBuilder.DropColumn(name: "From", schema: "dbclaim", table: "EmailOutboxItems");
            migrationBuilder.DropColumn(name: "From", schema: "dbclaim", table: "EmailLogs");
            migrationBuilder.DropColumn(name: "OccurredAtUtc", schema: "dbclaim", table: "AuditRecords");
        }
    }
}
