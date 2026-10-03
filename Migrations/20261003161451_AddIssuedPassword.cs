using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TSC.Migrations
{
    /// <inheritdoc />
    public partial class AddIssuedPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IssuedPassword",
                table: "AspNetUsers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IssuedPassword",
                table: "AspNetUsers");
        }
    }
}
