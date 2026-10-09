using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3SoftDeleteUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Units_NormalizedName",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_SubCategories_CategoryId_NormalizedName",
                table: "SubCategories");

            migrationBuilder.DropIndex(
                name: "IX_ProductModels_BrandId_NormalizedName",
                table: "ProductModels");

            migrationBuilder.DropIndex(
                name: "IX_Categories_NormalizedName",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Brands_NormalizedName",
                table: "Brands");

            migrationBuilder.CreateIndex(
                name: "IX_Units_NormalizedName",
                table: "Units",
                column: "NormalizedName",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_CategoryId_NormalizedName",
                table: "SubCategories",
                columns: new[] { "CategoryId", "NormalizedName" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProductModels_BrandId_NormalizedName",
                table: "ProductModels",
                columns: new[] { "BrandId", "NormalizedName" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_NormalizedName",
                table: "Categories",
                column: "NormalizedName",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Brands_NormalizedName",
                table: "Brands",
                column: "NormalizedName",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Units_NormalizedName",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_SubCategories_CategoryId_NormalizedName",
                table: "SubCategories");

            migrationBuilder.DropIndex(
                name: "IX_ProductModels_BrandId_NormalizedName",
                table: "ProductModels");

            migrationBuilder.DropIndex(
                name: "IX_Categories_NormalizedName",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Brands_NormalizedName",
                table: "Brands");

            migrationBuilder.CreateIndex(
                name: "IX_Units_NormalizedName",
                table: "Units",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_CategoryId_NormalizedName",
                table: "SubCategories",
                columns: new[] { "CategoryId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductModels_BrandId_NormalizedName",
                table: "ProductModels",
                columns: new[] { "BrandId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_NormalizedName",
                table: "Categories",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Brands_NormalizedName",
                table: "Brands",
                column: "NormalizedName",
                unique: true);
        }
    }
}
