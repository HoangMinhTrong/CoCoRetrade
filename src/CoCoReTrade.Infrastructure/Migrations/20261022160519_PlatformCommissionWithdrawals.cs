using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoCoReTrade.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlatformCommissionWithdrawals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminPayoutAccounts",
                columns: table => new
                {
                    AdminId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminPayoutAccounts", x => x.AdminId);
                    table.ForeignKey(
                        name: "FK_AdminPayoutAccounts_AspNetUsers_AdminId",
                        column: x => x.AdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformCommissionWithdrawals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,0)", nullable: false),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformCommissionWithdrawals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformCommissionWithdrawals_AspNetUsers_AdminId",
                        column: x => x.AdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformCommissionWithdrawals_AdminId_RequestKey",
                table: "PlatformCommissionWithdrawals",
                columns: new[] { "AdminId", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformCommissionWithdrawals_CreatedAtUtc",
                table: "PlatformCommissionWithdrawals",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminPayoutAccounts");

            migrationBuilder.DropTable(
                name: "PlatformCommissionWithdrawals");
        }
    }
}
