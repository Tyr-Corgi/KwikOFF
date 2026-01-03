using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KwikOff.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSecondarySearchTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "secondary_search_method",
                table: "comparison_results",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "used_secondary_search",
                table: "comparison_results",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "secondary_search_method",
                table: "comparison_results");

            migrationBuilder.DropColumn(
                name: "used_secondary_search",
                table: "comparison_results");
        }
    }
}
