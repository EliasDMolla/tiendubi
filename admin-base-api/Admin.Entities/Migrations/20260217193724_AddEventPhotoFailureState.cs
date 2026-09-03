using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Admin.Entities.Migrations
{
    /// <inheritdoc />
    public partial class AddEventPhotoFailureState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProcessingError",
                table: "EventPhotos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProcessingFailed",
                table: "EventPhotos",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessingError",
                table: "EventPhotos");

            migrationBuilder.DropColumn(
                name: "ProcessingFailed",
                table: "EventPhotos");
        }
    }
}
