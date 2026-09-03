using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Admin.Entities.Migrations
{
    [DbContext(typeof(Context))]
    [Migration("20260221121000_AddUserWithdrawalData")]
    public partial class AddUserWithdrawalData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WithdrawalAliasOrCbu",
                table: "Users",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WithdrawalBankName",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WithdrawalHolderName",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WithdrawalAliasOrCbu",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WithdrawalBankName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WithdrawalHolderName",
                table: "Users");
        }
    }
}
