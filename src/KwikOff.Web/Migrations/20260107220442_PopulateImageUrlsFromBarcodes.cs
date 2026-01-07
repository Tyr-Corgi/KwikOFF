using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KwikOff.Web.Migrations
{
    /// <inheritdoc />
    public partial class PopulateImageUrlsFromBarcodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create a function to construct OpenFoodFacts image URLs from barcodes
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION construct_off_image_url(barcode TEXT, image_type TEXT DEFAULT 'front', size TEXT DEFAULT '200')
                RETURNS TEXT AS $$
                DECLARE
                    clean_barcode TEXT;
                    barcode_path TEXT;
                    part1 TEXT;
                    part2 TEXT;
                    part3 TEXT;
                    part4 TEXT;
                BEGIN
                    -- Remove leading zeros
                    clean_barcode := LTRIM(barcode, '0');
                    IF clean_barcode = '' THEN
                        clean_barcode := '0';
                    END IF;
                    
                    -- Construct path based on barcode length
                    IF LENGTH(clean_barcode) >= 9 THEN
                        -- Split into groups of 3
                        part1 := SUBSTRING(clean_barcode FROM 1 FOR 3);
                        part2 := SUBSTRING(clean_barcode FROM 4 FOR 3);
                        part3 := SUBSTRING(clean_barcode FROM 7 FOR 3);
                        part4 := SUBSTRING(clean_barcode FROM 10);
                        
                        barcode_path := part1 || '/' || part2 || '/' || part3 || '/' || part4;
                    ELSE
                        barcode_path := clean_barcode;
                    END IF;
                    
                    -- Return the constructed URL
                    RETURN 'https://images.openfoodfacts.org/images/products/' || barcode_path || '/' || image_type || '_en.' || size || '.jpg';
                END;
                $$ LANGUAGE plpgsql IMMUTABLE;
            ");

            // Update all products without image URLs
            migrationBuilder.Sql(@"
                UPDATE open_food_facts_products
                SET 
                    image_url = construct_off_image_url(barcode, 'front', '400'),
                    image_small_url = construct_off_image_url(barcode, 'front', '200'),
                    image_front_url = construct_off_image_url(barcode, 'front', '400')
                WHERE 
                    (image_url IS NULL OR image_url = '')
                    AND barcode IS NOT NULL 
                    AND LENGTH(barcode) >= 8;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Clear the populated image URLs (optional - only if rolling back)
            migrationBuilder.Sql(@"
                UPDATE open_food_facts_products
                SET 
                    image_url = NULL,
                    image_small_url = NULL,
                    image_front_url = NULL
                WHERE 
                    image_url LIKE 'https://images.openfoodfacts.org/images/products/%/front_en.%.jpg';
            ");

            // Drop the helper function
            migrationBuilder.Sql(@"
                DROP FUNCTION IF EXISTS construct_off_image_url(TEXT, TEXT, TEXT);
            ");
        }
    }
}
