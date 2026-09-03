using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Admin.Entities.Migrations
{
    public partial class BackfillOrdersFromPhotoSales : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                FROM ""PhotoSales"" ps
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM ""Orders"" o
                    WHERE o.""PhotographerId"" = ps.""UserId""
                      AND o.""EventId"" = ps.""PhotographerEventId""
                      AND o.""CreatedAt"" = ps.""CreatedAt""
                      AND o.""TotalAmount"" = ps.""TotalAmount""
                );
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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""Orders"" o
                WHERE EXISTS (
                    SELECT 1
                    FROM ""PhotoSales"" ps
                    WHERE ps.""UserId"" = o.""PhotographerId""
                      AND ps.""PhotographerEventId"" = o.""EventId""
                      AND ps.""CreatedAt"" = o.""CreatedAt""
                      AND ps.""TotalAmount"" = o.""TotalAmount""
                );
            ");

            migrationBuilder.Sql(@"
                UPDATE ""PhotographerBalance"" b
                SET
                    ""PendingAmount"" = 0,
                    ""AvailableAmount"" = 0,
                    ""TotalWithdrawn"" = 0
                WHERE EXISTS (
                    SELECT 1
                    FROM ""Orders"" o
                    WHERE o.""PhotographerId"" = b.""PhotographerId""
                );
            ");
        }
    }
}
