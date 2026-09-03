using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Admin.Entities.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoDeliveryEmailTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeliveryEmailAttempts",
                table: "PhotoCheckoutSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryEmailError",
                table: "PhotoCheckoutSessions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryEmailLastAttemptAt",
                table: "PhotoCheckoutSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryEmailSentAt",
                table: "PhotoCheckoutSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryEmailStatus",
                table: "PhotoCheckoutSessions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NotSent");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryEmailAttempts",
                table: "PhotoCheckoutSessions");

            migrationBuilder.DropColumn(
                name: "DeliveryEmailError",
                table: "PhotoCheckoutSessions");

            migrationBuilder.DropColumn(
                name: "DeliveryEmailLastAttemptAt",
                table: "PhotoCheckoutSessions");

            migrationBuilder.DropColumn(
                name: "DeliveryEmailSentAt",
                table: "PhotoCheckoutSessions");

            migrationBuilder.DropColumn(
                name: "DeliveryEmailStatus",
                table: "PhotoCheckoutSessions");
        }
    }
}
