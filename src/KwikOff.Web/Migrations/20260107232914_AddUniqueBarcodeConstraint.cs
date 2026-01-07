using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KwikOff.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueBarcodeConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add unique constraint to prevent duplicate barcodes per tenant
            migrationBuilder.CreateIndex(
                name: "IX_imported_products_tenant_id_barcode",
                table: "imported_products",
                columns: new[] { "tenant_id", "barcode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the unique constraint
            migrationBuilder.DropIndex(
                name: "IX_imported_products_tenant_id_barcode",
                table: "imported_products");
        }
    }
}
