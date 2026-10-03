using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TSC.Migrations
{
    /// <inheritdoc />
    public partial class ClassesAndBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "Batches", newName: "SchoolClasses");
            migrationBuilder.RenameTable(name: "BatchSubjects", newName: "ClassSubjects");

            migrationBuilder.RenameColumn(name: "BatchId", table: "Students", newName: "SchoolClassId");
            migrationBuilder.RenameColumn(name: "BatchId", table: "Exams", newName: "SchoolClassId");
            migrationBuilder.RenameColumn(name: "BatchId", table: "ClassSubjects", newName: "SchoolClassId");

            migrationBuilder.RenameIndex(name: "IX_Students_BatchId", table: "Students", newName: "IX_Students_SchoolClassId");
            migrationBuilder.RenameIndex(name: "IX_Exams_BatchId", table: "Exams", newName: "IX_Exams_SchoolClassId");
            migrationBuilder.RenameIndex(name: "IX_BatchSubjects_BatchId_SubjectId", table: "ClassSubjects", newName: "IX_ClassSubjects_SchoolClassId_SubjectId");
            migrationBuilder.RenameIndex(name: "IX_BatchSubjects_SubjectId", table: "ClassSubjects", newName: "IX_ClassSubjects_SubjectId");
            migrationBuilder.RenameIndex(name: "IX_BatchSubjects_TeacherId", table: "ClassSubjects", newName: "IX_ClassSubjects_TeacherId");

            migrationBuilder.AddColumn<int>(
                name: "Level", table: "SchoolClasses", type: "integer", nullable: false, defaultValue: 0);

            // Existing rows are named 'Class 2' .. 'Class 5'. Pull the digits out of the name.
            // Anything that does not match is left at 0 and is caught by the check below.
            migrationBuilder.Sql(@"
                UPDATE ""SchoolClasses""
                SET ""Level"" = CAST(substring(""Name"" from '[0-9]+') AS integer)
                WHERE ""Name"" ~ '[0-9]+';");

            // A class left at level 0 would silently generate Student IDs like TSC0001 forever.
            // Fail the migration instead, so a badly named class is fixed by a person.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM ""SchoolClasses"" WHERE ""Level"" = 0) THEN
                        RAISE EXCEPTION 'A class has no level. Rename it to contain its number (for example ''Class 3'') and re-run.';
                    END IF;
                    IF EXISTS (SELECT ""Level"" FROM ""SchoolClasses"" GROUP BY ""Level"" HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Two classes share a level. Student IDs would collide.';
                    END IF;
                END $$;");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolClasses_Level", table: "SchoolClasses", column: "Level", unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_SchoolClasses_Level", table: "SchoolClasses");
            migrationBuilder.DropColumn(name: "Level", table: "SchoolClasses");

            migrationBuilder.RenameIndex(name: "IX_ClassSubjects_TeacherId", table: "ClassSubjects", newName: "IX_BatchSubjects_TeacherId");
            migrationBuilder.RenameIndex(name: "IX_ClassSubjects_SubjectId", table: "ClassSubjects", newName: "IX_BatchSubjects_SubjectId");
            migrationBuilder.RenameIndex(name: "IX_ClassSubjects_SchoolClassId_SubjectId", table: "ClassSubjects", newName: "IX_BatchSubjects_BatchId_SubjectId");
            migrationBuilder.RenameIndex(name: "IX_Exams_SchoolClassId", table: "Exams", newName: "IX_Exams_BatchId");
            migrationBuilder.RenameIndex(name: "IX_Students_SchoolClassId", table: "Students", newName: "IX_Students_BatchId");

            migrationBuilder.RenameColumn(name: "SchoolClassId", table: "ClassSubjects", newName: "BatchId");
            migrationBuilder.RenameColumn(name: "SchoolClassId", table: "Exams", newName: "BatchId");
            migrationBuilder.RenameColumn(name: "SchoolClassId", table: "Students", newName: "BatchId");

            migrationBuilder.RenameTable(name: "ClassSubjects", newName: "BatchSubjects");
            migrationBuilder.RenameTable(name: "SchoolClasses", newName: "Batches");
        }
    }
}
