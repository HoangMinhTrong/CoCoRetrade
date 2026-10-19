using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoCoReTrade.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlatformCommissionLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformCommissionEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,0)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformCommissionEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformCommissionEntries_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformCommissionEntries_CreatedAtUtc",
                table: "PlatformCommissionEntries",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformCommissionEntries_OrderId",
                table: "PlatformCommissionEntries",
                column: "OrderId",
                unique: true);

            // Existing completed orders already contributed to the revenue report. Carry their
            // immutable commission snapshots into the platform ledger when this table is added.
            migrationBuilder.Sql("""
                INSERT INTO [PlatformCommissionEntries] ([Id], [OrderId], [Amount], [CreatedAtUtc])
                SELECT NEWID(), [o].[Id], [o].[CommissionAmount],
                       COALESCE([o].[CompletedAtUtc], SYSUTCDATETIME())
                FROM [Orders] AS [o]
                WHERE [o].[Status] = 'Completed' AND [o].[PaymentStatus] = 'Paid'
                  AND [o].[ItemAmount] > 0 AND [o].[CommissionAmount] >= 0
                  AND [o].[CommissionAmount] < [o].[ItemAmount]
                  AND NOT EXISTS (SELECT 1 FROM [PlatformCommissionEntries] AS [c]
                                  WHERE [c].[OrderId] = [o].[Id]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformCommissionEntries");
        }
    }
}
