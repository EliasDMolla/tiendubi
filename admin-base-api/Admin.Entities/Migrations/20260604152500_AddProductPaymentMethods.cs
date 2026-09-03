using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Admin.Entities.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(Context))]
    [Migration("20260604152500_AddProductPaymentMethods")]
    public partial class AddProductPaymentMethods : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "PhotographerEvents"
                ADD COLUMN IF NOT EXISTS "PaymentMethods" character varying(100) NOT NULL DEFAULT 'mercadopago';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "PhotographerEvents"
                DROP COLUMN IF EXISTS "PaymentMethods";
                """);
        }
    }
}
