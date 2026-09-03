using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Admin.Entities.Migrations
{
    /// <inheritdoc />
    public partial class AddEventPhotoProcessingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProcessed",
                table: "EventPhotos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OriginalPath",
                table: "EventPhotos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailPath",
                table: "EventPhotos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WatermarkedPath",
                table: "EventPhotos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsProcessed",
                table: "EventPhotos");

            migrationBuilder.DropColumn(
                name: "OriginalPath",
                table: "EventPhotos");

            migrationBuilder.DropColumn(
                name: "ThumbnailPath",
                table: "EventPhotos");

            migrationBuilder.DropColumn(
                name: "WatermarkedPath",
                table: "EventPhotos");
        }
    }
}
