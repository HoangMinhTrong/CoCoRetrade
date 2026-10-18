using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoCoReTrade.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SkuGeneratedOnPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Discard unpublished inventory codes created by the previous workflow.
            // Published/reserved/sold products keep their already-issued official SKU.
            migrationBuilder.Sql("UPDATE [Products] SET [Sku] = N'' WHERE [Status] IN (N'PendingApproval', N'Rejected') AND [Sku] <> N'';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Old provisional SKU values cannot be reconstructed safely.
        }
    }
}
