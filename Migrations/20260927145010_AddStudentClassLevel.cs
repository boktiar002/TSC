using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TSC.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentClassLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClassLevel",
                table: "Students",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClassLevel",
                table: "Students");
        }
    }
}
