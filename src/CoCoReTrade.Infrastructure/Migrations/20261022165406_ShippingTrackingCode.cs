using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoCoReTrade.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ShippingTrackingCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrackingCode",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrackingCode",
                table: "Orders");
        }
    }
}
