using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TSC.Migrations
{
    /// <inheritdoc />
    public partial class VoidPaymentsInsteadOfDeleting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVoided",
                table: "Payments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidedByUserId",
                table: "Payments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVoided",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VoidedByUserId",
                table: "Payments");
        }
    }
}
