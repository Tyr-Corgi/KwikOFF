using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KwikOff.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "imported_products",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    product_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    brand = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    category = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    supplier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    internal_sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    sales_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    allergens = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    unit_of_measure = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    traceability_lot_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    origin_location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    current_location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    destination_location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    harvest_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pack_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ship_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    receive_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expiration_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reference_document_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reference_document_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    import_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    row_number = table.Column<int>(type: "integer", nullable: false),
                    imported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    original_data = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_imported_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "open_food_facts_products",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    product_name = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    generic_name = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    brands = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    categories = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    categories_tags = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ingredients_text = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    allergens = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    allergens_tags = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    traces = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    traces_tags = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    nutrition_grades = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    nova_group = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ecoscore = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    energy_kcal100g = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    fat100g = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    saturated_fat100g = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    carbohydrates100g = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    sugars100g = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    fiber100g = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    proteins100g = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    salt100g = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    sodium100g = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    serving_size = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    serving_quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    labels = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    labels_tags = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    stores = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    countries = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    countries_tags = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    image_small_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    image_front_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    image_ingredients_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    image_nutrition_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    packaging = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    packaging_tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    quantity = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    origins = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    origins_tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    manufacturing_places = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    creator = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    completeness = table.Column<int>(type: "integer", nullable: true),
                    raw_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_open_food_facts_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sync_statuses",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    source_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_syncing = table.Column<bool>(type: "boolean", nullable: false),
                    last_sync_started = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_sync_completed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_products_synced = table.Column<long>(type: "bigint", nullable: false),
                    current_batch_count = table.Column<long>(type: "bigint", nullable: false),
                    progress_percentage = table.Column<int>(type: "integer", nullable: false),
                    status_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    last_error_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    downloaded_bytes = table.Column<long>(type: "bigint", nullable: true),
                    total_bytes = table.Column<long>(type: "bigint", nullable: true),
                    last_updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_sync_statuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_column_mappings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_pattern = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    column_mapping_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_by_user = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    use_count = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_tenant_column_mappings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "comparison_results",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    imported_product_id = table.Column<long>(type: "bigint", nullable: false),
                    open_food_facts_product_id = table.Column<long>(type: "bigint", nullable: true),
                    match_status = table.Column<int>(type: "integer", nullable: false),
                    confidence_score = table.Column<double>(type: "double precision", precision: 5, scale: 4, nullable: false),
                    comparison_details = table.Column<string>(type: "jsonb", nullable: true),
                    has_name_discrepancy = table.Column<bool>(type: "boolean", nullable: false),
                    has_brand_discrepancy = table.Column<bool>(type: "boolean", nullable: false),
                    has_category_discrepancy = table.Column<bool>(type: "boolean", nullable: false),
                    has_allergen_discrepancy = table.Column<bool>(type: "boolean", nullable: false),
                    has_nutrition_discrepancy = table.Column<bool>(type: "boolean", nullable: false),
                    compared_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    comparison_batch_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_comparison_results", x => x.id);
                    table.ForeignKey(
                        name: "f_k_comparison_results__imported_products_imported_product_id",
                        column: x => x.imported_product_id,
                        principalTable: "imported_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_comparison_results__open_food_facts_products_open_food_facts_pr~",
                        column: x => x.open_food_facts_product_id,
                        principalTable: "open_food_facts_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_comparison_results_compared_at",
                table: "comparison_results",
                column: "compared_at");

            migrationBuilder.CreateIndex(
                name: "ix_comparison_results_comparison_batch_id",
                table: "comparison_results",
                column: "comparison_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_comparison_results_imported_product_id",
                table: "comparison_results",
                column: "imported_product_id");

            migrationBuilder.CreateIndex(
                name: "ix_comparison_results_match_status",
                table: "comparison_results",
                column: "match_status");

            migrationBuilder.CreateIndex(
                name: "ix_comparison_results_openfoodfacts_product_id",
                table: "comparison_results",
                column: "open_food_facts_product_id");

            migrationBuilder.CreateIndex(
                name: "ix_comparison_results_product_date",
                table: "comparison_results",
                columns: new[] { "imported_product_id", "compared_at" });

            migrationBuilder.CreateIndex(
                name: "ix_imported_products_barcode",
                table: "imported_products",
                column: "barcode");

            migrationBuilder.CreateIndex(
                name: "ix_imported_products_brand",
                table: "imported_products",
                column: "brand");

            migrationBuilder.CreateIndex(
                name: "ix_imported_products_import_batch_id",
                table: "imported_products",
                column: "import_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_imported_products_imported_at",
                table: "imported_products",
                column: "imported_at");

            migrationBuilder.CreateIndex(
                name: "ix_imported_products_normalized_barcode",
                table: "imported_products",
                column: "normalized_barcode");

            migrationBuilder.CreateIndex(
                name: "ix_imported_products_tenant_barcode",
                table: "imported_products",
                columns: new[] { "tenant_id", "normalized_barcode" });

            migrationBuilder.CreateIndex(
                name: "ix_imported_products_tenant_id",
                table: "imported_products",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_imported_products_tlc",
                table: "imported_products",
                column: "traceability_lot_code");

            migrationBuilder.CreateIndex(
                name: "ix_openfoodfacts_products_barcode",
                table: "open_food_facts_products",
                column: "barcode");

            migrationBuilder.CreateIndex(
                name: "ix_openfoodfacts_products_brands",
                table: "open_food_facts_products",
                column: "brands");

            migrationBuilder.CreateIndex(
                name: "ix_openfoodfacts_products_last_modified",
                table: "open_food_facts_products",
                column: "last_modified");

            migrationBuilder.CreateIndex(
                name: "ix_openfoodfacts_products_normalized_barcode",
                table: "open_food_facts_products",
                column: "normalized_barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_openfoodfacts_products_nutrition_grades",
                table: "open_food_facts_products",
                column: "nutrition_grades");

            migrationBuilder.CreateIndex(
                name: "ix_sync_statuses_is_syncing",
                table: "sync_statuses",
                column: "is_syncing");

            migrationBuilder.CreateIndex(
                name: "ix_sync_statuses_source_name",
                table: "sync_statuses",
                column: "source_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_column_mappings_last_used",
                table: "tenant_column_mappings",
                column: "last_used_at");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_column_mappings_tenant_active",
                table: "tenant_column_mappings",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_column_mappings_tenant_id",
                table: "tenant_column_mappings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_column_mappings_tenant_pattern",
                table: "tenant_column_mappings",
                columns: new[] { "tenant_id", "file_pattern" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comparison_results");

            migrationBuilder.DropTable(
                name: "sync_statuses");

            migrationBuilder.DropTable(
                name: "tenant_column_mappings");

            migrationBuilder.DropTable(
                name: "imported_products");

            migrationBuilder.DropTable(
                name: "open_food_facts_products");
        }
    }
}
