using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Admin.Entities.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPublishingFieldsGenerated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuyerInstructions",
                table: "PhotographerEvents",
                type: "character varying(3000)",
                maxLength: 3000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverImagePath",
                table: "PhotographerEvents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryLink",
                table: "PhotographerEvents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalPrice",
                table: "PhotographerEvents",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceType",
                table: "PhotographerEvents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "paid");

            migrationBuilder.AddColumn<string>(
                name: "ProductType",
                table: "PhotographerEvents",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "digital_file");

            migrationBuilder.CreateTable(
                name: "ProductAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PhotographerEventId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductAssets_PhotographerEvents_PhotographerEventId",
                        column: x => x.PhotographerEventId,
                        principalTable: "PhotographerEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductAssets_PhotographerEventId",
                table: "ProductAssets",
                column: "PhotographerEventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductAssets");

            migrationBuilder.DropColumn(
                name: "BuyerInstructions",
                table: "PhotographerEvents");

            migrationBuilder.DropColumn(
                name: "CoverImagePath",
                table: "PhotographerEvents");

            migrationBuilder.DropColumn(
                name: "DeliveryLink",
                table: "PhotographerEvents");

            migrationBuilder.DropColumn(
                name: "OriginalPrice",
                table: "PhotographerEvents");

            migrationBuilder.DropColumn(
                name: "PriceType",
                table: "PhotographerEvents");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "PhotographerEvents");
        }
    }
}
