using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElixomClaim.Lib.Migrations
{
    /// <inheritdoc />
    public partial class AddJobPaymentAdjustmentApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdjustmentReason",
                schema: "dbclaim",
                table: "JobPayments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                schema: "dbclaim",
                table: "JobPayments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                schema: "dbclaim",
                table: "JobPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecoveryReceivable",
                schema: "dbclaim",
                table: "JobPayments",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdjustmentReason",
                schema: "dbclaim",
                table: "JobPayments");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                schema: "dbclaim",
                table: "JobPayments");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                schema: "dbclaim",
                table: "JobPayments");

            migrationBuilder.DropColumn(
                name: "IsRecoveryReceivable",
                schema: "dbclaim",
                table: "JobPayments");
        }
    }
}
