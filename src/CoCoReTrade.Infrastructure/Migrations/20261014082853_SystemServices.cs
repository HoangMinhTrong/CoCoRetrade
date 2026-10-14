using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoCoReTrade.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SystemServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM WithdrawalRequests WHERE LEN([Status]) > 20 OR LEN([BankAccountMasked]) > 60) OR EXISTS (SELECT 1 FROM WalletTransactions WHERE LEN([Type]) > 450) THROW 51000, 'Existing wallet text exceeds the new limits.', 1;");
            migrationBuilder.Sql("UPDATE WithdrawalRequests SET [Status] = 'Pending' WHERE [Status] = '';");
            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_OrderId",
                table: "WalletTransactions");
            migrationBuilder.DropIndex(
                name: "IX_WithdrawalRequests_WalletId",
                table: "WithdrawalRequests");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "WithdrawalRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "BankAccountMasked",
                table: "WithdrawalRequests",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "DecidedAtUtc",
                table: "WithdrawalRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecidedById",
                table: "WithdrawalRequests",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionReason",
                table: "WithdrawalRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestKey",
                table: "WithdrawalRequests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "WalletTransactions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "WithdrawalRequestId",
                table: "WalletTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboxMessages_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShippingZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Province = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Ward = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsInnerCity = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingZones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_DecidedById",
                table: "WithdrawalRequests",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_WalletId_RequestKey",
                table: "WithdrawalRequests",
                columns: new[] { "WalletId", "RequestKey" },
                unique: true,
                filter: "[RequestKey] <> '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_WithdrawalRequestId_Type",
                table: "WalletTransactions",
                columns: new[] { "WithdrawalRequestId", "Type" },
                unique: true,
                filter: "[WithdrawalRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_OrderId",
                table: "WalletTransactions",
                column: "OrderId",
                unique: true,
                filter: "[OrderId] IS NOT NULL AND [Type] = 'SellerPayout'");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_NotificationId_Channel",
                table: "OutboxMessages",
                columns: new[] { "NotificationId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_SentAtUtc_NextAttemptAtUtc_LeaseUntilUtc",
                table: "OutboxMessages",
                columns: new[] { "SentAtUtc", "NextAttemptAtUtc", "LeaseUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ShippingZones_Province_Ward",
                table: "ShippingZones",
                columns: new[] { "Province", "Ward" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransactions_WithdrawalRequests_WithdrawalRequestId",
                table: "WalletTransactions",
                column: "WithdrawalRequestId",
                principalTable: "WithdrawalRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequests_AspNetUsers_DecidedById",
                table: "WithdrawalRequests",
                column: "DecidedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_OrderId",
                table: "WalletTransactions");
            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransactions_WithdrawalRequests_WithdrawalRequestId",
                table: "WalletTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequests_AspNetUsers_DecidedById",
                table: "WithdrawalRequests");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "ShippingZones");

            migrationBuilder.DropIndex(
                name: "IX_WithdrawalRequests_DecidedById",
                table: "WithdrawalRequests");

            migrationBuilder.DropIndex(
                name: "IX_WithdrawalRequests_WalletId_RequestKey",
                table: "WithdrawalRequests");

            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_WithdrawalRequestId_Type",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "DecidedAtUtc",
                table: "WithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "DecidedById",
                table: "WithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "DecisionReason",
                table: "WithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "RequestKey",
                table: "WithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "WithdrawalRequestId",
                table: "WalletTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "WithdrawalRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "BankAccountMasked",
                table: "WithdrawalRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(60)",
                oldMaxLength: 60);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "WalletTransactions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_WalletId",
                table: "WithdrawalRequests",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_OrderId",
                table: "WalletTransactions",
                column: "OrderId",
                unique: true,
                filter: "[OrderId] IS NOT NULL AND [Type] = 'SellerPayout'");
        }
    }
}
