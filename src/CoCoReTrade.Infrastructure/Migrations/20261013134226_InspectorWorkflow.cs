using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoCoReTrade.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InspectorWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Sku",
                table: "Products");

            migrationBuilder.AddColumn<string>(
                name: "ConsignmentCode",
                table: "Products",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "LatestInspectionId",
                table: "Products",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InspectionId",
                table: "ProductImages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AskingPrice",
                table: "Inspections",
                type: "decimal(18,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "Inspections",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Inspections",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Inspections",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalPrice",
                table: "Inspections",
                type: "decimal(18,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Size",
                table: "Inspections",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "VerifiedCondition",
                table: "Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "AuditLogs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            // Old listings received an SKU at submission. Preserve it as the drop-off code;
            // pending/rejected listings receive a new inventory SKU only after inspection.
            migrationBuilder.Sql("UPDATE [Products] SET [ConsignmentCode] = [Sku]");
            migrationBuilder.Sql("UPDATE [Products] SET [Sku] = '' WHERE [Status] IN ('PendingApproval', 'Rejected')");
            migrationBuilder.Sql("""
                UPDATE i SET [Name] = p.[Name], [Brand] = p.[Brand], [Category] = p.[Category],
                    [Size] = p.[Size], [OriginalPrice] = p.[OriginalPrice], [AskingPrice] = p.[AskingPrice],
                    [VerifiedCondition] = COALESCE(p.[VerifiedCondition], p.[DeclaredCondition])
                FROM [Inspections] i INNER JOIN [Products] p ON i.[ProductId] = p.[Id];
                UPDATE p SET [LatestInspectionId] =
                    (SELECT TOP(1) i.[Id] FROM [Inspections] i WHERE i.[ProductId] = p.[Id]
                     ORDER BY i.[CreatedAtUtc] DESC, i.[Id] DESC)
                FROM [Products] p;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Products_ConsignmentCode",
                table: "Products",
                column: "ConsignmentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true,
                filter: "[Sku] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_InspectionId",
                table: "ProductImages",
                column: "InspectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductImages_Inspections_InspectionId",
                table: "ProductImages",
                column: "InspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductImages_Inspections_InspectionId",
                table: "ProductImages");

            migrationBuilder.DropIndex(
                name: "IX_Products_ConsignmentCode",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Sku",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductImages_InspectionId",
                table: "ProductImages");

            migrationBuilder.Sql("UPDATE [Products] SET [Sku] = [ConsignmentCode] WHERE [Sku] = ''");

            migrationBuilder.DropColumn(
                name: "ConsignmentCode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LatestInspectionId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "InspectionId",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "AskingPrice",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "OriginalPrice",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "VerifiedCondition",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true);
        }
    }
}
