using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Admin.Entities.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPublicSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicSlug",
                table: "Users",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PublicSlug",
                table: "Users",
                column: "PublicSlug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_PublicSlug",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PublicSlug",
                table: "Users");
        }
    }
}
