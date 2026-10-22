using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoCoReTrade.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PreliminaryProductReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreliminaryReason",
                table: "Products",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreliminaryReviewedAtUtc",
                table: "Products",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreliminaryStatus",
                table: "Products",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.Sql("UPDATE [Products] SET [PreliminaryStatus] = 'Approved' WHERE [LatestInspectionId] IS NOT NULL OR [Status] IN ('Active', 'Reserved', 'Sold')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreliminaryReason",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PreliminaryReviewedAtUtc",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PreliminaryStatus",
                table: "Products");
        }
    }
}
