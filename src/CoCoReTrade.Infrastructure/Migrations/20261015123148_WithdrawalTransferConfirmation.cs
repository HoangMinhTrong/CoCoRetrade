using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoCoReTrade.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WithdrawalTransferConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                table: "WithdrawalRequests",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "WithdrawalRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "WithdrawalRequests",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "TransferReference",
                table: "WithdrawalRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TransferredAtUtc",
                table: "WithdrawalRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferredById",
                table: "WithdrawalRequests",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_TransferredById",
                table: "WithdrawalRequests",
                column: "TransferredById");

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequests_AspNetUsers_TransferredById",
                table: "WithdrawalRequests",
                column: "TransferredById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequests_AspNetUsers_TransferredById",
                table: "WithdrawalRequests");

            migrationBuilder.DropIndex(
                name: "IX_WithdrawalRequests_TransferredById",
                table: "WithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                table: "WithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "WithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "WithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "TransferReference",
                table: "WithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "TransferredAtUtc",
                table: "WithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "TransferredById",
                table: "WithdrawalRequests");
        }
    }
}
