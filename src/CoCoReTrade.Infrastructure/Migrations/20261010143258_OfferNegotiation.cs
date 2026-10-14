using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoCoReTrade.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OfferNegotiation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Offers_ProductId",
                table: "Offers");

            migrationBuilder.DropIndex(
                name: "IX_OfferEvents_OfferId",
                table: "OfferEvents");

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionAmount",
                table: "Orders",
                type: "decimal(18,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "OfferId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientName",
                table: "Orders",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecipientPhone",
                table: "Orders",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ShippingAddress",
                table: "Orders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedPrice",
                table: "Offers",
                type: "decimal(18,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AskingPrice",
                table: "Offers",
                type: "decimal(18,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Offers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "LastBuyerPrice",
                table: "Offers",
                type: "decimal(18,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LastSellerPrice",
                table: "Offers",
                type: "decimal(18,0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "Offers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProductSku",
                table: "Offers",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE o SET
                    AskingPrice = p.AskingPrice,
                    LastBuyerPrice = o.CurrentPrice,
                    ProductName = p.Name,
                    ProductSku = p.Sku,
                    CreatedAtUtc = CASE WHEN o.ExpiresAtUtc > '2000-01-02' THEN DATEADD(hour, -24, o.ExpiresAtUtc) ELSE SYSUTCDATETIME() END,
                    AcceptedPrice = CASE WHEN o.Status = 'Accepted' THEN o.CurrentPrice ELSE NULL END
                FROM Offers AS o INNER JOIN Products AS p ON p.Id = o.ProductId;
                IF EXISTS (SELECT 1 FROM OfferEvents WHERE [Action] NOT IN ('Submitted', 'SellerCountered', 'BuyerCountered', 'Accepted', 'Declined', 'Ordered'))
                    THROW 51000, 'Review existing OfferEvents.Action values before applying OfferNegotiation migration.', 1;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "OfferEvents",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "OfferId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OfferId",
                table: "Orders",
                column: "OfferId",
                unique: true,
                filter: "[OfferId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_ProductId_BuyerId",
                table: "Offers",
                columns: new[] { "ProductId", "BuyerId" });

            migrationBuilder.CreateIndex(
                name: "IX_OfferEvents_OfferId_Action",
                table: "OfferEvents",
                columns: new[] { "OfferId", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OfferId",
                table: "Notifications",
                column: "OfferId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Offers_OfferId",
                table: "Notifications",
                column: "OfferId",
                principalTable: "Offers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Offers_OfferId",
                table: "Orders",
                column: "OfferId",
                principalTable: "Offers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Offers_OfferId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Offers_OfferId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OfferId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Offers_ProductId_BuyerId",
                table: "Offers");

            migrationBuilder.DropIndex(
                name: "IX_OfferEvents_OfferId_Action",
                table: "OfferEvents");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_OfferId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CommissionAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OfferId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RecipientName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RecipientPhone",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingAddress",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AcceptedPrice",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "AskingPrice",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "LastBuyerPrice",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "LastSellerPrice",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "ProductName",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "ProductSku",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "OfferId",
                table: "Notifications");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "OfferEvents",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.CreateIndex(
                name: "IX_Offers_ProductId",
                table: "Offers",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferEvents_OfferId",
                table: "OfferEvents",
                column: "OfferId");
        }
    }
}
