using CoCoReTrade.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoCoReTrade.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261015160000_NotificationDestinations")]
public partial class NotificationDestinations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ConversationId", table: "Notifications", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "ProductId", table: "Notifications", type: "uniqueidentifier", nullable: true);

        migrationBuilder.CreateIndex(name: "IX_Notifications_ConversationId", table: "Notifications", column: "ConversationId");
        migrationBuilder.CreateIndex(name: "IX_Notifications_ProductId", table: "Notifications", column: "ProductId");

        migrationBuilder.AddForeignKey(
            name: "FK_Notifications_Conversations_ConversationId", table: "Notifications", column: "ConversationId",
            principalTable: "Conversations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_Notifications_Products_ProductId", table: "Notifications", column: "ProductId",
            principalTable: "Products", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Notifications_Conversations_ConversationId", table: "Notifications");
        migrationBuilder.DropForeignKey(name: "FK_Notifications_Products_ProductId", table: "Notifications");
        migrationBuilder.DropIndex(name: "IX_Notifications_ConversationId", table: "Notifications");
        migrationBuilder.DropIndex(name: "IX_Notifications_ProductId", table: "Notifications");
        migrationBuilder.DropColumn(name: "ConversationId", table: "Notifications");
        migrationBuilder.DropColumn(name: "ProductId", table: "Notifications");
    }
}
