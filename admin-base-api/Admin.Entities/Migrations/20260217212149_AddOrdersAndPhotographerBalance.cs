using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Admin.Entities.Migrations
{
    /// <inheritdoc />
    public partial class AddOrdersAndPhotographerBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PhotographerId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PlatformCommission = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MercadoPagoFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PhotographerNet = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ClearedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_PhotographerEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "PhotographerEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Orders_Users_PhotographerId",
                        column: x => x.PhotographerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhotographerBalance",
                columns: table => new
                {
                    PhotographerId = table.Column<int>(type: "integer", nullable: false),
                    PendingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    AvailableAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    TotalWithdrawn = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotographerBalance", x => x.PhotographerId);
                    table.ForeignKey(
                        name: "FK_PhotographerBalance_Users_PhotographerId",
                        column: x => x.PhotographerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_EventId",
                table: "Orders",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PhotographerId_CreatedAt",
                table: "Orders",
                columns: new[] { "PhotographerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PhotographerId_EventId",
                table: "Orders",
                columns: new[] { "PhotographerId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PhotographerId_Status_ClearedAt",
                table: "Orders",
                columns: new[] { "PhotographerId", "Status", "ClearedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotographerBalance_PhotographerId",
                table: "PhotographerBalance",
                column: "PhotographerId",
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO ""Orders"" (
                    ""PhotographerId"",
                    ""EventId"",
                    ""TotalAmount"",
                    ""PlatformCommission"",
                    ""MercadoPagoFee"",
                    ""PhotographerNet"",
                    ""Status"",
                    ""ClearedAt"",
                    ""CreatedAt"",
                    ""UpdatedAt""
                )
                SELECT
                    ps.""UserId"",
                    ps.""PhotographerEventId"",
                    ps.""TotalAmount"",
                    0,
                    0,
                    ps.""TotalAmount"",
                    CASE
                        WHEN LOWER(ps.""Status"") = 'paidout' THEN 'PaidOut'
                        ELSE 'Paid'
                    END,
                    (ps.""SoldAt"" + INTERVAL '72 hours'),
                    ps.""CreatedAt"",
                    ps.""UpdatedAt""
                FROM ""PhotoSales"" ps;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""PhotographerBalance"" (""PhotographerId"", ""PendingAmount"", ""AvailableAmount"", ""TotalWithdrawn"")
                SELECT
                    o.""PhotographerId"",
                    COALESCE(SUM(CASE WHEN o.""Status"" = 'Paid' AND o.""ClearedAt"" > NOW() THEN o.""PhotographerNet"" ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN o.""Status"" = 'Paid' AND o.""ClearedAt"" <= NOW() THEN o.""PhotographerNet"" ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN o.""Status"" = 'PaidOut' THEN o.""PhotographerNet"" ELSE 0 END), 0)
                FROM ""Orders"" o
                GROUP BY o.""PhotographerId""
                ON CONFLICT (""PhotographerId"") DO UPDATE
                SET
                    ""PendingAmount"" = EXCLUDED.""PendingAmount"",
                    ""AvailableAmount"" = EXCLUDED.""AvailableAmount"",
                    ""TotalWithdrawn"" = EXCLUDED.""TotalWithdrawn"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "PhotographerBalance");
        }
    }
}
