using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Admin.Entities.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesModuleLiquidationsAndOrderColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_PhotographerId_Status_ClearedAt",
                table: "Orders");

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidOutAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PhotoId",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Liquidations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PhotographerId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    FromDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ToDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Liquidations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Liquidations_Users_PhotographerId",
                        column: x => x.PhotographerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PhotographerId_ClearedAt",
                table: "Orders",
                columns: new[] { "PhotographerId", "ClearedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PhotoId",
                table: "Orders",
                column: "PhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status",
                table: "Orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Liquidations_PhotographerId",
                table: "Liquidations",
                column: "PhotographerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_EventPhotos_PhotoId",
                table: "Orders",
                column: "PhotoId",
                principalTable: "EventPhotos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_EventPhotos_PhotoId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "Liquidations");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PhotographerId_ClearedAt",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PhotoId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Status",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaidOutAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PhotoId",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PhotographerId_Status_ClearedAt",
                table: "Orders",
                columns: new[] { "PhotographerId", "Status", "ClearedAt" });
        }
    }
}
