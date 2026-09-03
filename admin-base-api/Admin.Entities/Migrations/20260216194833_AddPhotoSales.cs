using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Admin.Entities.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhotoSales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PhotographerEventId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BuyerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    BuyerEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PaymentMethod = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "manual"),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "paid"),
                    SoldAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoSales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotoSales_PhotographerEvents_PhotographerEventId",
                        column: x => x.PhotographerEventId,
                        principalTable: "PhotographerEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhotoSales_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoSales_PhotographerEventId",
                table: "PhotoSales",
                column: "PhotographerEventId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoSales_SoldAt",
                table: "PhotoSales",
                column: "SoldAt");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoSales_UserId",
                table: "PhotoSales",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhotoSales");
        }
    }
}
