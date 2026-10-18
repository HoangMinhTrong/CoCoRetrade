using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoCoReTrade.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AcceptedOfferPickupDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PickupDeadlineAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE o SET PickupDeadlineAtUtc = f.ExpiresAtUtc
                FROM Orders AS o
                INNER JOIN Offers AS f ON f.Id = o.OfferId
                WHERE o.Fulfillment = 'Pickup' AND o.PickupDeadlineAtUtc IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PickupDeadlineAtUtc",
                table: "Orders",
                column: "PickupDeadlineAtUtc",
                filter: "[PickupDeadlineAtUtc] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_PickupDeadlineAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PickupDeadlineAtUtc",
                table: "Orders");
        }
    }
}
