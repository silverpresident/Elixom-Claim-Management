using ElixomClaim.Lib.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElixomClaim.Lib.Migrations;

/// <inheritdoc />
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260903110000_OAuthHardeningAndConsents")]
public partial class OAuthHardeningAndConsents : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbclaim].[OAuthAuthorizationCodes]') AND name = N'Code') ALTER TABLE [dbclaim].[OAuthAuthorizationCodes] DROP COLUMN [Code];");

        migrationBuilder.CreateTable(
            name: "OAuthConsents",
            schema: "dbclaim",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Scope = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                GrantedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OAuthConsents", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OAuthConsents_UserId_ClientId",
            schema: "dbclaim",
            table: "OAuthConsents",
            columns: new[] { "UserId", "ClientId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OAuthConsents",
            schema: "dbclaim");

        migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbclaim].[OAuthAuthorizationCodes]') AND name = N'Code') ALTER TABLE [dbclaim].[OAuthAuthorizationCodes] ADD [Code] nvarchar(256) NULL;");
    }
}
