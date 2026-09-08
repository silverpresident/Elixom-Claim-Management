using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElixomClaim.Lib.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxWakeUpProcessingLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingStartedAtUtc",
                schema: "dbclaim",
                table: "OperationRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationRecords_OperationType_Status_ProcessingStartedAtUtc",
                schema: "dbclaim",
                table: "OperationRecords",
                columns: new[] { "OperationType", "Status", "ProcessingStartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OperationRecords_OperationType_Status_ProcessingStartedAtUtc",
                schema: "dbclaim",
                table: "OperationRecords");

            migrationBuilder.DropColumn(
                name: "ProcessingStartedAtUtc",
                schema: "dbclaim",
                table: "OperationRecords");
        }
    }
}
