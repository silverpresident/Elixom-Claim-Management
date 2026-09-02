using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElixomClaim.Lib.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditAndOAuthEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditRecords",
                schema: "dbclaim",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ActorEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Target = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BeforeStateJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterStateJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsMcpOperation = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OAuthAuthorizationCodes",
                schema: "dbclaim",
                columns: table => new
                {
                    CodeHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RedirectUri = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CodeChallenge = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CodeChallengeMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthAuthorizationCodes", x => x.CodeHash);
                });

            migrationBuilder.CreateTable(
                name: "OAuthClients",
                schema: "dbclaim",
                columns: table => new
                {
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientSecretHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RedirectUrisJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AllowedGrantTypes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AllowedScopes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthClients", x => x.ClientId);
                });

            migrationBuilder.CreateTable(
                name: "OAuthTokens",
                schema: "dbclaim",
                columns: table => new
                {
                    TokenHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TokenId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TokenType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RefreshTokenFamilyId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthTokens", x => x.TokenHash);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditRecords",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "OAuthAuthorizationCodes",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "OAuthClients",
                schema: "dbclaim");

            migrationBuilder.DropTable(
                name: "OAuthTokens",
                schema: "dbclaim");
        }
    }
}
