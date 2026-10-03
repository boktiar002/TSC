using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TSC.Migrations
{
    /// <inheritdoc />
    public partial class DropStudentClassLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ClassLevel held the class number as text ("3"), duplicating SchoolClass.Level.
            // Nothing wrote it after the SchoolClass rename, so every student added since
            // then has it empty. The class now comes from the SchoolClass navigation.
            migrationBuilder.DropColumn(
                name: "ClassLevel",
                table: "Students");

            // Which branch the child attends -- Morning or Evening. Not a rename of
            // ClassLevel: that column held class numbers, which would be nonsense here.
            migrationBuilder.AddColumn<string>(
                name: "Batch",
                table: "Students",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Batch",
                table: "Students");

            migrationBuilder.AddColumn<string>(
                name: "ClassLevel",
                table: "Students",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }
    }
}
