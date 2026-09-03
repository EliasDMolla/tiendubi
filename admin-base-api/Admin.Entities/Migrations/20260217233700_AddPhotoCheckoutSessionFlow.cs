using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Admin.Entities.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoCheckoutSessionFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhotoCheckoutSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExternalReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PhotographerId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    PhotoIdsCsv = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    BuyerEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BuyerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DiscountCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SubtotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PreferenceId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    MerchantOrderId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Created"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoCheckoutSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotoCheckoutSessions_PhotographerEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "PhotographerEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhotoCheckoutSessions_Users_PhotographerId",
                        column: x => x.PhotographerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoCheckoutSessions_EventId",
                table: "PhotoCheckoutSessions",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoCheckoutSessions_ExternalReference",
                table: "PhotoCheckoutSessions",
                column: "ExternalReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhotoCheckoutSessions_PhotographerId_CreatedAt",
                table: "PhotoCheckoutSessions",
                columns: new[] { "PhotographerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoCheckoutSessions_PreferenceId",
                table: "PhotoCheckoutSessions",
                column: "PreferenceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhotoCheckoutSessions");
        }
    }
}
