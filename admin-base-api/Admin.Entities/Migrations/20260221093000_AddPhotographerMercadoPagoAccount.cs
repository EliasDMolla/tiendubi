using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Admin.Entities.Migrations
{
    [DbContext(typeof(Context))]
    [Migration("20260221093000_AddPhotographerMercadoPagoAccount")]
    public partial class AddPhotographerMercadoPagoAccount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhotographerMercadoPagoAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PhotographerId = table.Column<int>(type: "integer", nullable: false),
                    AccessToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RefreshToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    PublicKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MercadoPagoUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TokenExpiration = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotographerMercadoPagoAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotographerMercadoPagoAccounts_Users_PhotographerId",
                        column: x => x.PhotographerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhotographerMercadoPagoAccounts_MercadoPagoUserId",
                table: "PhotographerMercadoPagoAccounts",
                column: "MercadoPagoUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotographerMercadoPagoAccounts_PhotographerId",
                table: "PhotographerMercadoPagoAccounts",
                column: "PhotographerId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhotographerMercadoPagoAccounts");
        }
    }
}
