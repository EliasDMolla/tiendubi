using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Admin.Entities.Migrations
{
    public partial class SeedOwnerSuperUser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO ""Users"" (
    ""Email"",
    ""PasswordHash"",
    ""FullName"",
    ""IsActive"",
    ""EmailVerified"",
    ""Role"",
    ""PlanType"",
    ""Plan"",
    ""SubscriptionStatus"",
    ""UsageTypeId"",
    ""CreatedAt"",
    ""UpdatedAt""
)
VALUES (
    'elias.molla.cel@gmail.com',
    '$2b$10$fGXlD0z/fWji.Uz28YVH2Oz4EarM63sOGNI/oZwM7wzUYm3jnN9zm',
    'Elias Molla',
    TRUE,
    TRUE,
    2,
    2,
    'PRO',
    'ACTIVO',
    1,
    NOW(),
    NOW()
)
ON CONFLICT (""Email"") DO UPDATE
SET
    ""PasswordHash"" = EXCLUDED.""PasswordHash"",
    ""FullName"" = EXCLUDED.""FullName"",
    ""IsActive"" = TRUE,
    ""EmailVerified"" = TRUE,
    ""Role"" = 2,
    ""PlanType"" = 2,
    ""Plan"" = 'PRO',
    ""SubscriptionStatus"" = 'ACTIVO',
    ""UsageTypeId"" = 1,
    ""UpdatedAt"" = NOW();
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM ""Users""
WHERE ""Email"" = 'elias.molla.cel@gmail.com';
");
        }
    }
}
