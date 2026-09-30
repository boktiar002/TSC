# Phase 1: Schema and Rename — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the schema match the centre's vocabulary — classes 2–5 as the academic unit, morning/afternoon as the batch — and add every column the later phases need, with no visible change to the app.

**Architecture:** A mechanical rename of `Batch`→`SchoolClass` across 50 files, then four migrations applied in order. Existing data is preserved by hand-editing the generated migrations to use `RenameTable`/`RenameColumn` rather than the drop-and-create EF will otherwise scaffold. Two pure helpers (`PhoneNumber`, `Dues`) are added with self-tests because they carry rules that fail silently.

**Tech Stack:** ASP.NET Core 10 MVC, EF Core 10, Npgsql/PostgreSQL 18, Bootstrap 5.3. No test framework — assertions run through `dotnet run -- selftest`, following `GradingSelfTest.cs`.

**Spec:** `docs/superpowers/specs/2026-10-01-tsc-professional-overhaul-design.md`

## Global Constraints

- Target framework `net10.0`. **No new NuGet packages in this phase.**
- Tests are `Check(...)` assertions in a `*SelfTest.cs` class invoked from `Program.cs` under `dotnet run -- selftest`. No xUnit, no NSubstitute, no in-memory EF provider.
- Migrations must **preserve existing data**: 87 students, their marks, attendance and payments. Any generated migration containing `DropTable` or `DropColumn` for a renamed object is wrong and must be hand-edited.
- Student ID format is `TSC` + 2-digit level + 2-digit serial (`TSC0317`), per `Data/imports/renumber_student_ids.sql`.
- Batch values are exactly `"Morning"` and `"Afternoon"`, as `private static readonly string[]`, following `PaymentsController.Methods`.
- Payment kinds are exactly `"Monthly"` and `"Admission"`.
- `Student.Batch` is nullable — the 87 existing students have no shift recorded.
- **Deviation from the spec:** the spec names two migrations; this plan uses four
  (`ClassesAndBatches`, `StudentBatch`, `ProfessionalIntake`, `NormalizeGuardianPhones`).
  Splitting them is what lets each task end in a state that can be applied, verified
  against the database and rolled back on its own. The end schema is identical.
- The app must be stopped before building. A running instance locks `bin\Debug\net10.0\TSC.exe` and the build fails with MSB3027.

## Review Focus

These are the phase-1 failure modes the spec implies but that no task's main flow exercises. Each has a test pinned to the task that owns the code.

1. **A `Batches` row whose name is not `"Class N"`** — `Level` backfills to `0`, silently, and every Student ID generated afterwards becomes `TSC00xx`. Owned by Task 3.
2. **A guardian phone written `+8801712345678` or `01712-345678`** — normalization must land on `01712345678` or the guardian's password will never match their login. Owned by Task 1.
3. **A null, empty or nonsense guardian phone** — normalization must return null rather than throw or produce a partial string. Owned by Task 1.
4. **An `Admission` payment in the month a guardian checks dues** — must not reduce that month's tuition balance, or a parent is told they have paid when they have not. Owned by Task 7.
5. **Two `Batches` rows that map to the same `Level`** — would collide every future Student ID for that class. Owned by Task 3.

---

## File Structure

**Created:**
- `Data/PhoneNumber.cs` — normalize a Bangladeshi mobile to 11 digits. Pure.
- `PhoneSelfTest.cs` — assertions for the above.
- `Data/Dues.cs` — payment-kind constants and the monthly-balance rule. Pure arithmetic plus one shared query filter.
- `DuesSelfTest.cs` — assertions for the above.
- 4 files under `Migrations/`.

**Renamed:**
- `Models/Batch.cs` → `Models/SchoolClass.cs`
- `Models/BatchSubject.cs` → `Models/ClassSubject.cs`

**Modified:** `Models/Student.cs`, `Models/Exam.cs`, `Models/Teacher.cs`, `Models/Subject.cs`, `Models/RollCall.cs`, `Models/ApplicationUser.cs`, `Models/Payment.cs`, `Data/ApplicationDbContext.cs`, `Data/Ranking.cs`, `Data/RollCallBook.cs`, `Data/MarkEntry.cs`, `Program.cs`, 8 controllers, ~30 views.

---

### Task 1: Phone normalization

**Files:**
- Create: `Data/PhoneNumber.cs`
- Create: `PhoneSelfTest.cs`
- Modify: `Program.cs` (selftest dispatch)

**Interfaces:**
- Consumes: nothing.
- Produces: `TSC.Data.PhoneNumber.Normalize(string? raw) → string?` — returns an 11-digit `01`-prefixed string, or `null` when the input cannot be one.

- [ ] **Step 1: Write the failing test**

Create `PhoneSelfTest.cs`:

```csharp
using System.Runtime.CompilerServices;
using TSC.Data;

// Run with: dotnet run -- selftest
internal static class PhoneSelfTest
{
    public static void Run()
    {
        // The canonical form: 11 digits starting 01.
        Check(PhoneNumber.Normalize("01712345678") == "01712345678");

        // Country code, with and without the plus.
        Check(PhoneNumber.Normalize("+8801712345678") == "01712345678");
        Check(PhoneNumber.Normalize("8801712345678") == "01712345678");
        Check(PhoneNumber.Normalize("+880 1712 345678") == "01712345678");

        // Punctuation and spacing the office actually types.
        Check(PhoneNumber.Normalize("01712-345678") == "01712345678");
        Check(PhoneNumber.Normalize("01712 345678") == "01712345678");
        Check(PhoneNumber.Normalize("  01712345678  ") == "01712345678");

        // Every Bangladeshi mobile operator prefix.
        Check(PhoneNumber.Normalize("01312345678") == "01312345678");
        Check(PhoneNumber.Normalize("01912345678") == "01912345678");

        // Not a mobile number: return null rather than a partial string, so the caller
        // refuses to create a login instead of creating one nobody can use.
        Check(PhoneNumber.Normalize(null) == null);
        Check(PhoneNumber.Normalize("") == null);
        Check(PhoneNumber.Normalize("   ") == null);
        Check(PhoneNumber.Normalize("0171234567") == null);      // 10 digits
        Check(PhoneNumber.Normalize("017123456789") == null);    // 12 digits
        Check(PhoneNumber.Normalize("02123456789") == null);     // landline, not 01
        Check(PhoneNumber.Normalize("hello") == null);
        Check(PhoneNumber.Normalize("+8802123456789") == null);

        Console.WriteLine("phone selftest: all checks passed");
    }

    private static void Check(bool condition, [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition)
            throw new Exception($"phone selftest FAILED: {expression}");
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Stop the running app first, then:

```bash
dotnet run -- selftest
```

Expected: compile error — `PhoneNumber` does not exist.

- [ ] **Step 3: Write the implementation**

Create `Data/PhoneNumber.cs`:

```csharp
namespace TSC.Data;

// A guardian's mobile number is the password to their child's portal, so the form it is
// stored in and the form it is typed in at sign-in have to agree exactly. Everything is
// reduced to the 11-digit national form: 01XXXXXXXXX.
public static class PhoneNumber
{
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var digits = new string(raw.Where(char.IsDigit).ToArray());

        // +8801712345678 and 8801712345678 both carry the country code.
        if (digits.Length == 13 && digits.StartsWith("880"))
            digits = "0" + digits[3..];

        // Bangladeshi mobiles are 11 digits and always start 01.
        return digits.Length == 11 && digits.StartsWith("01") ? digits : null;
    }
}
```

- [ ] **Step 4: Wire it into the selftest runner**

In `Program.cs`, replace:

```csharp
if (args.Contains("selftest"))
{
    GradingSelfTest.Run();
    return 0;
}
```

with:

```csharp
if (args.Contains("selftest"))
{
    GradingSelfTest.Run();
    PhoneSelfTest.Run();
    return 0;
}
```

- [ ] **Step 5: Run to verify it passes**

```bash
dotnet run -- selftest
```

Expected: `grading selftest: all checks passed` then `phone selftest: all checks passed`.

- [ ] **Step 6: Commit**

```bash
git add Data/PhoneNumber.cs PhoneSelfTest.cs Program.cs
git commit -m "Normalize Bangladeshi mobile numbers to 01XXXXXXXXX"
```

---

### Task 2: Rename Batch to SchoolClass

Mechanical and wide. The build either succeeds or it does not — that is the test. Do not change behaviour anywhere in this task.

**Files:**
- Rename: `Models/Batch.cs` → `Models/SchoolClass.cs`, `Models/BatchSubject.cs` → `Models/ClassSubject.cs`
- Modify: `Data/ApplicationDbContext.cs`, `Data/Ranking.cs`, `Data/RollCallBook.cs`, `Data/MarkEntry.cs`, `Models/Student.cs`, `Models/Exam.cs`, `Models/Teacher.cs`, `Models/Subject.cs`, `Models/RollCall.cs`, all 8 controllers, ~30 views
- Rename: `Areas/Admin/Controllers/BatchesController.cs` → `ClassesController.cs`, `Areas/Admin/Views/Batches/` → `Areas/Admin/Views/Classes/`

**Interfaces:**
- Produces: `SchoolClass` entity with `Id`, `Name`, `Level` (int), `MonthlyFee`, `Description`, `Students`, `ClassSubjects`, `Exams`. `ClassSubject` with `SchoolClassId`/`SchoolClass`. `Student.SchoolClassId`/`Student.SchoolClass`. `Exam.SchoolClassId`/`Exam.SchoolClass`. `ApplicationDbContext.SchoolClasses`, `.ClassSubjects`.

- [ ] **Step 1: Rename the entity files and types**

`git mv Models/Batch.cs Models/SchoolClass.cs`, then make it:

```csharp
using System.ComponentModel.DataAnnotations;

namespace TSC.Models;

// One of the four classes the centre runs: 2, 3, 4 and 5. Morning and afternoon are not
// separate classes -- both branches sit the same paper and are ranked together, so exams,
// subjects, fees, ranking and attendance are all per class. Which branch a child attends
// is Student.Batch.
public class SchoolClass
{
    public int Id { get; set; }

    [Required, StringLength(60), Display(Name = "Class Name")]
    public string Name { get; set; } = string.Empty;

    // 2, 3, 4 or 5. Student IDs and sorting read this rather than parsing "Class 3".
    [Range(1, 12), Display(Name = "Class Level")]
    public int Level { get; set; }

    [Range(0, 1000000), Display(Name = "Monthly Fee")]
    public decimal MonthlyFee { get; set; }

    [StringLength(250)]
    public string? Description { get; set; }

    public ICollection<Student> Students { get; set; } = new List<Student>();

    public ICollection<ClassSubject> ClassSubjects { get; set; } = new List<ClassSubject>();

    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
}
```

`git mv Models/BatchSubject.cs Models/ClassSubject.cs`, then make it:

```csharp
namespace TSC.Models;

public class ClassSubject
{
    public int Id { get; set; }

    public int SchoolClassId { get; set; }
    public SchoolClass? SchoolClass { get; set; }

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
}
```

- [ ] **Step 2: Apply the rename everywhere else**

Order matters — the longer identifiers first, or `Batch`→`SchoolClass` will corrupt `BatchSubject`.

```bash
cd "F:/Big Projects/TSC"
files=$(grep -rl "Batch" --include=*.cs --include=*.cshtml . | grep -v "^./bin\|^./obj\|^./.vs\|^./Migrations")
for f in $files; do
  sed -i \
    -e 's/BatchSubjects/ClassSubjects/g' \
    -e 's/BatchSubject/ClassSubject/g' \
    -e 's/BatchId/SchoolClassId/g' \
    -e 's/\bBatches\b/SchoolClasses/g' \
    -e 's/\bBatch\b/SchoolClass/g' \
    -e 's/\bbatchId\b/schoolClassId/g' \
    -e 's/\bbatches\b/schoolClasses/g' \
    -e 's/\bbatch\b/schoolClass/g' \
    -e 's/MyBatches/MyClasses/g' \
    -e 's/LoadBatches/LoadSchoolClasses/g' \
    -e 's/BatchCount/SchoolClassCount/g' \
    "$f"
done
```

The last three rules exist because `\bBatches\b` will not match inside `LoadBatches` or
`MyBatches` — there is no word boundary between `Load` and `Batches`. Without them the
build fails on identifiers that are half-renamed. After running, search for any other
compound that slipped through:

```bash
grep -rn "Batch" --include=*.cs --include=*.cshtml . | grep -v "^./bin\|^./obj\|^./Migrations"
```

- [ ] **Step 3: Rename the controller and view folder**

```bash
git mv Areas/Admin/Controllers/BatchesController.cs Areas/Admin/Controllers/ClassesController.cs
git mv Areas/Admin/Views/Batches Areas/Admin/Views/Classes
```

Then in `ClassesController.cs` change the class name to `ClassesController`.

- [ ] **Step 4: Fix what sed got wrong**

The blunt replace will have damaged user-facing English and route names. Check each of these by hand:

- Every `asp-controller="SchoolClasses"` must become `asp-controller="Classes"`.
- Every `asp-route-schoolClassId` must become `asp-route-schoolClassId` only where the action parameter is genuinely named that; the route values must match the action signatures.
- Display strings that now read "SchoolClass" must read "Class": `[Display(Name = "SchoolClass")]`, `-- Select SchoolClass --`, `"Please select a schoolClass."`, nav labels, page headings.
- `Views/Shared/_Layout.cshtml` nav item pointing at `Batches`.
- `Areas/Teacher/Views/Home/Batch.cshtml` — `git mv` it to `Class.cshtml` and rename the `Batch` action in `Areas/Teacher/Controllers/HomeController.cs` to `Class`, updating the two `asp-action="Batch"` references.

Find the survivors:

```bash
grep -rn "SchoolClass" --include=*.cshtml . | grep -v "^./bin\|^./obj" | grep -iE "select|display|>Class|label|placeholder"
```

- [ ] **Step 5: Verify it compiles**

```bash
dotnet build -t:Compile --nologo -v q
```

Expected: `Build succeeded.` with no warnings. `-t:Compile` skips the copy to `bin`, so this works even if the app is running.

- [ ] **Step 6: Verify nothing was missed**

```bash
grep -rn "\bBatch\b\|BatchId\|BatchSubject" --include=*.cs --include=*.cshtml . | grep -v "^./bin\|^./obj\|^./Migrations"
```

Expected: no output. `Migrations/` is excluded deliberately — old migrations describe the schema as it was and must not be rewritten.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "Rename Batch to SchoolClass throughout

The database's Batches rows are the centre's classes -- 'Class 3' and so on.
What the centre calls a batch is morning or afternoon, which had no column at
all. Renaming the entity frees the name Batch for the shift, so code and the
centre's vocabulary agree.

Mechanical rename. No behaviour change."
```

---

### Task 3: Migration — rename the tables and add Level

**Files:**
- Create: `Migrations/<timestamp>_ClassesAndBatches.cs`
- Modify: `Data/ApplicationDbContext.cs`

**Interfaces:**
- Consumes: `SchoolClass`, `ClassSubject` from Task 2.
- Produces: tables `SchoolClasses`, `ClassSubjects`; columns `Students.SchoolClassId`, `Exams.SchoolClassId`, `ClassSubjects.SchoolClassId`, `SchoolClasses.Level` populated with 2–5.

- [ ] **Step 1: Update the DbContext index definitions**

In `Data/ApplicationDbContext.cs`, the sed pass will have produced `SchoolClasses`/`ClassSubject` names already. Confirm these two lines read:

```csharp
builder.Entity<SchoolClass>().HasIndex(c => c.Name).IsUnique();
builder.Entity<ClassSubject>().HasIndex(cs => new { cs.SchoolClassId, cs.SubjectId }).IsUnique();
```

And add, below them:

```csharp
// Two classes on the same level would generate colliding Student IDs.
builder.Entity<SchoolClass>().HasIndex(c => c.Level).IsUnique();
```

- [ ] **Step 2: Scaffold the migration**

```bash
dotnet ef migrations add ClassesAndBatches
```

- [ ] **Step 3: Hand-edit it — this is the step that saves the data**

Open the generated `Migrations/<timestamp>_ClassesAndBatches.cs`. EF will very likely have scaffolded `DropTable`/`CreateTable` and `DropColumn`/`AddColumn`, because it cannot tell a rename from a delete-plus-create. **That would destroy all 87 students, their marks, attendance and payments.**

Replace the entire `Up` body with:

```csharp
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
```

And the `Down` body with the same operations reversed:

```csharp
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
```

Confirm the real index names first — they must match what is in the database:

```bash
psql -h localhost -U postgres -d coaching_center -c "\d \"Students\"" -c "\d \"BatchSubjects\"" -c "\d \"Exams\""
```

Adjust any `RenameIndex` name that does not match. Foreign key constraint names are renamed automatically by Postgres when the table is renamed, so they need no operations here.

- [ ] **Step 4: Back up, then apply**

```bash
pg_dump -h localhost -U postgres coaching_center > ~/coaching_center_pre_phase1.sql
dotnet ef database update
```

- [ ] **Step 5: Verify the data survived**

```bash
psql -h localhost -U postgres -d coaching_center -c \
  'SELECT "Id", "Name", "Level", "MonthlyFee" FROM "SchoolClasses" ORDER BY "Level";'
psql -h localhost -U postgres -d coaching_center -c \
  'SELECT count(*) AS students FROM "Students";'
psql -h localhost -U postgres -d coaching_center -c \
  'SELECT count(*) AS marks FROM "Marks";'
```

Expected: every class has a non-zero `Level` matching its name; student count is 87; the mark count matches what it was before the migration.

- [ ] **Step 6: Commit**

```bash
git add Migrations/ Data/ApplicationDbContext.cs
git commit -m "Migration: rename Batches to SchoolClasses, add Level

Hand-edited to RenameTable/RenameColumn. The scaffolded version drops and
recreates, which would take every student, mark, attendance row and payment
with it.

Level is backfilled from the class name and the migration refuses to complete
if any class is left at 0 or two classes share a level -- either would silently
produce colliding Student IDs later."
```

---

### Task 4: Replace ClassLevel with SchoolClass.Level, add Student.Batch

**Files:**
- Modify: `Models/Student.cs`, `Areas/Admin/Controllers/StudentsController.cs`, `Areas/Admin/Views/Students/Index.cshtml`, `Areas/Admin/Views/Students/Create.cshtml`, `Areas/Admin/Views/Students/Edit.cshtml`, `Areas/Admin/Views/Students/Details.cshtml`, `Areas/Admin/Views/Students/Delete.cshtml`, `Areas/Student/Views/Home/Index.cshtml`, `Areas/Admin/Views/Payments/Receipt.cshtml`, `Data/ApplicationDbContext.cs`

**Interfaces:**
- Consumes: `SchoolClass.Level` from Task 3.
- Produces: `Student.Batch` (`string?`), `Student.ClassLevel` removed. `StudentsController.Batches` (`static readonly string[]`).

- [ ] **Step 1: Change the model**

In `Models/Student.cs`, delete:

```csharp
    [StringLength(20)]
    public string? ClassLevel { get; set; }
```

and add, next to `SchoolClassId`:

```csharp
    // Morning or afternoon. Which branch the child attends -- not an academic division:
    // both branches sit the same paper and are ranked together.
    // Nullable because the students already on the roll have no branch recorded.
    [StringLength(10)]
    public string? Batch { get; set; }
```

- [ ] **Step 2: Find every use of ClassLevel**

```bash
grep -rn "ClassLevel\|classLevel" --include=*.cs --include=*.cshtml . | grep -v "^./bin\|^./obj\|^./Migrations"
```

- [ ] **Step 3: Replace them**

In `StudentsController.Index`, the signature keeps a filter but it becomes an int level. Replace the `classLevel` parameter and its two blocks:

```csharp
    public async Task<IActionResult> Index(bool archived = false, string? q = null,
        int? schoolClassId = null, int? level = null)
```

```csharp
        if (level is > 0)
            query = query.Where(s => s.SchoolClass!.Level == level);
```

```csharp
        ViewBag.Level = level;
        ViewBag.Levels = await _context.SchoolClasses
            .OrderBy(c => c.Level)
            .Select(c => c.Level)
            .ToListAsync();
```

In `StudentsController.Edit`, delete the line `existingStudent.ClassLevel = student.ClassLevel;` and add `existingStudent.Batch = student.Batch;`.

Add to the top of `StudentsController`:

```csharp
    public static readonly string[] Batches = { "Morning", "Afternoon" };
```

In `LoadSchoolClasses`, also expose them:

```csharp
    private async Task LoadSchoolClasses()
    {
        ViewBag.SchoolClasses = await _context.SchoolClasses
            .OrderBy(c => c.Level)
            .ToListAsync();

        ViewBag.Batches = Batches;
    }
```

In every view, `@student.ClassLevel` becomes `@student.SchoolClass?.Level`, and the hand-written class `<select>` in `Create.cshtml` and `Edit.cshtml` (the hard-coded `<option value="2">Class 2</option>` list) is deleted — the class is already chosen by the `SchoolClassId` dropdown directly above it, which is where the duplication came from in the first place. Add a batch select in its place:

```html
<div class="col-md-6 mb-3">
    <label asp-for="Batch" class="form-label">Batch</label>
    <select asp-for="Batch" class="form-select">
        <option value="">-- Select Batch --</option>
        @foreach (var b in (string[])ViewBag.Batches)
        {
            <option value="@b">@b</option>
        }
    </select>
    <span asp-validation-for="Batch" class="text-danger"></span>
</div>
```

Views that print a student's class in a heading — `Details.cshtml`, `Delete.cshtml`, `Areas/Student/Views/Home/Index.cshtml`, `Areas/Admin/Views/Payments/Receipt.cshtml` — show the batch alongside it:

```html
Class @Model.SchoolClass?.Level @(Model.Batch is null ? "" : $"· {Model.Batch}")
```

Any query reading a student for one of those views needs `.Include(s => s.SchoolClass)`. `Details`, `Delete` and the payment `Receipt` already include it; check `Areas/Student/Controllers/HomeController.MeAsync` does too — it does.

- [ ] **Step 4: Verify it compiles**

```bash
dotnet build -t:Compile --nologo -v q
grep -rn "ClassLevel" --include=*.cs --include=*.cshtml . | grep -v "^./bin\|^./obj\|^./Migrations"
```

Expected: `Build succeeded.`, and no remaining `ClassLevel` outside `Migrations/`.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Replace Student.ClassLevel with SchoolClass.Level, add Student.Batch

ClassLevel duplicated the class name with nothing keeping the two in sync -- a
student could sit in Class 5 with ClassLevel '3' and no validation objected.
The class now has one source of truth. Batch holds morning or afternoon."
```

---

### Task 5: Migration — drop ClassLevel, add Batch

**Files:**
- Create: `Migrations/<timestamp>_StudentBatch.cs`

**Interfaces:**
- Consumes: `Student.Batch`, `Student.ClassLevel` removed, from Task 4.
- Produces: column `Students.Batch` (`text`, nullable); `Students.ClassLevel` gone.

- [ ] **Step 1: Scaffold**

```bash
dotnet ef migrations add StudentBatch
```

- [ ] **Step 2: Check what it generated**

Open it. It should contain exactly an `AddColumn` for `Batch` and a `DropColumn` for `ClassLevel` — here a real drop is correct, because the data genuinely moves to `SchoolClasses.Level`, which Task 3 already populated.

Confirm before dropping that no student disagrees with their class — if any do, the class is authoritative and the mismatch is worth seeing:

```bash
psql -h localhost -U postgres -d coaching_center -c \
  'SELECT s."StudentId", s."ClassLevel", c."Level"
   FROM "Students" s JOIN "SchoolClasses" c ON c."Id" = s."SchoolClassId"
   WHERE s."ClassLevel" IS DISTINCT FROM c."Level"::text;'
```

Expected: no rows. If there are rows, that is the bug this change exists to kill — note the student IDs, then proceed; `SchoolClasses.Level` is correct.

- [ ] **Step 3: Apply**

```bash
dotnet ef database update
```

- [ ] **Step 4: Verify**

```bash
psql -h localhost -U postgres -d coaching_center -c '\d "Students"'
```

Expected: a `Batch` column of type `text`, nullable; no `ClassLevel` column.

- [ ] **Step 5: Commit**

```bash
git add Migrations/
git commit -m "Migration: drop Students.ClassLevel, add Students.Batch"
```

---

### Task 6: Add the columns the later phases need

**Files:**
- Modify: `Models/Student.cs`, `Models/Payment.cs`, `Models/ApplicationUser.cs`
- Create: `Migrations/<timestamp>_ProfessionalIntake.cs`
- Modify: `.gitignore`

**Interfaces:**
- Produces: `Student.PhotoPath`, `.Gender`, `.BloodGroup`, `.PreviousSchool`, `.GuardianRelation`, `.GuardianOccupation`, `.AlternatePhone`, `.AdmissionDate`, `.SessionYear` (computed). `Payment.Kind`, `.RecordedByUserId`. `ApplicationUser.IsPortalAccount`.

- [ ] **Step 1: Add the Student columns**

In `Models/Student.cs`, after `DateOfBirth`:

```csharp
    [StringLength(10)]
    public string? Gender { get; set; }

    [StringLength(5), Display(Name = "Blood Group")]
    public string? BloodGroup { get; set; }

    [StringLength(120), Display(Name = "Previous School")]
    public string? PreviousSchool { get; set; }

    [StringLength(30), Display(Name = "Guardian Relation")]
    public string? GuardianRelation { get; set; }

    [StringLength(60), Display(Name = "Guardian Occupation")]
    public string? GuardianOccupation { get; set; }

    [Phone, StringLength(20), Display(Name = "Alternate Phone")]
    public string? AlternatePhone { get; set; }

    [DataType(DataType.Date), Display(Name = "Admission Date")]
    public DateOnly AdmissionDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    // Relative path under wwwroot, not the bytes.
    [StringLength(200)]
    public string? PhotoPath { get; set; }

    // Not a column: derivable for every real case. It becomes one the day a student is
    // admitted in December for the following session.
    public int SessionYear => AdmissionDate.Year;
```

`SessionYear` is computed and EF would try to map it. Add to `OnModelCreating` in `ApplicationDbContext`:

```csharp
builder.Entity<Student>().Ignore(s => s.SessionYear);
```

- [ ] **Step 2: Add the Payment columns**

In `Models/Payment.cs`:

```csharp
    // "Monthly" or "Admission". An admission fee is recorded against the month it was
    // taken, but is not part of what that month's tuition costs -- without this it would
    // silently cancel out a student's monthly balance.
    [StringLength(20)]
    public string Kind { get; set; } = Dues.Monthly;

    // Teachers can record payments, so the ledger records who took the money.
    public string? RecordedByUserId { get; set; }
```

Add `using TSC.Data;` at the top of the file.

- [ ] **Step 3: Add the user flag**

In `Models/ApplicationUser.cs`:

```csharp
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    // A guardian's password is their phone number -- all digits. The password policy is
    // relaxed for these accounts only; admin and teacher accounts keep full strength.
    // Set before CreateAsync, because the role is not attached until after it.
    public bool IsPortalAccount { get; set; }
}
```

- [ ] **Step 4: Ignore the uploads folder**

Append to `.gitignore`:

```
## Student photos -- personal data, not source.
wwwroot/uploads/
```

- [ ] **Step 5: Scaffold and apply**

`Dues.Monthly` does not exist yet, so create `Data/Dues.cs` now with just the constants (Task 7 fills in the rest):

```csharp
namespace TSC.Data;

public static class Dues
{
    public const string Monthly = "Monthly";
    public const string Admission = "Admission";
}
```

Then:

```bash
dotnet build -t:Compile --nologo -v q
dotnet ef migrations add ProfessionalIntake
dotnet ef database update
```

- [ ] **Step 6: Verify**

```bash
psql -h localhost -U postgres -d coaching_center -c '\d "Students"' -c '\d "Payments"'
psql -h localhost -U postgres -d coaching_center -c \
  'SELECT DISTINCT "Kind" FROM "Payments";'
```

Expected: the new `Students` columns present; `Payments` has `Kind` and `RecordedByUserId`; every existing payment row reads `Monthly`.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "Add admission, payment-kind and portal-account columns"
```

---

### Task 7: The monthly-balance rule

Four separate queries sum a month's payments to work out what is owed. All four would count an admission fee as tuition. Fix it once, in a shared place, rather than four times.

**Files:**
- Modify: `Data/Dues.cs`
- Create: `DuesSelfTest.cs`
- Modify: `Program.cs`, `Areas/Admin/Controllers/PaymentsController.cs`, `Areas/Student/Controllers/HomeController.cs`

**Interfaces:**
- Consumes: `Payment.Kind` from Task 6.
- Produces: `Dues.MonthlyOnly(IQueryable<Payment>) → IQueryable<Payment>`, `Dues.Outstanding(decimal fee, decimal paid) → decimal`.

- [ ] **Step 1: Write the failing test**

Create `DuesSelfTest.cs`:

```csharp
using System.Runtime.CompilerServices;
using TSC.Data;
using TSC.Models;

// Run with: dotnet run -- selftest
internal static class DuesSelfTest
{
    public static void Run()
    {
        // The balance never goes negative: overpaying leaves nothing outstanding.
        Check(Dues.Outstanding(700m, 0m) == 700m);
        Check(Dues.Outstanding(700m, 300m) == 400m);
        Check(Dues.Outstanding(700m, 700m) == 0m);
        Check(Dues.Outstanding(700m, 900m) == 0m);
        Check(Dues.Outstanding(0m, 0m) == 0m);

        // An admission fee must not count towards a month's tuition. Without the filter a
        // guardian who paid a 500 admission fee is told their 700 monthly fee is nearly
        // settled when nothing has been paid towards it.
        var payments = new List<Payment>
        {
            new() { Amount = 500m, Kind = Dues.Admission },
            new() { Amount = 200m, Kind = Dues.Monthly },
        }.AsQueryable();

        Check(Dues.MonthlyOnly(payments).Sum(p => p.Amount) == 200m);
        Check(Dues.Outstanding(700m, Dues.MonthlyOnly(payments).Sum(p => p.Amount)) == 500m);

        // A payment written before Kind existed defaults to Monthly and still counts.
        var legacy = new List<Payment> { new() { Amount = 700m } }.AsQueryable();
        Check(Dues.MonthlyOnly(legacy).Sum(p => p.Amount) == 700m);

        Console.WriteLine("dues selftest: all checks passed");
    }

    private static void Check(bool condition, [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition)
            throw new Exception($"dues selftest FAILED: {expression}");
    }
}
```

- [ ] **Step 2: Run to verify it fails**

```bash
dotnet run -- selftest
```

Expected: compile error — `Dues.MonthlyOnly` and `Dues.Outstanding` do not exist.

- [ ] **Step 3: Implement**

Replace `Data/Dues.cs` with:

```csharp
using TSC.Models;

namespace TSC.Data;

// What a student owes for a month, worked out in one place. Four different screens ask
// this question -- the office ledger, the receipt, the payment prefill and the guardian's
// own fee page -- and an admission fee must not answer any of them.
public static class Dues
{
    public const string Monthly = "Monthly";
    public const string Admission = "Admission";

    public static readonly string[] Kinds = { Monthly, Admission };

    // Only tuition counts towards a month's balance.
    public static IQueryable<Payment> MonthlyOnly(IQueryable<Payment> payments) =>
        payments.Where(p => p.Kind == Monthly);

    // Paying more than the fee leaves nothing owed, never a negative balance.
    public static decimal Outstanding(decimal fee, decimal paid) => Math.Max(0, fee - paid);
}
```

- [ ] **Step 4: Route the four call sites through it**

`Areas/Admin/Controllers/PaymentsController.cs`, in `Index`:

```csharp
        ViewBag.PaidByStudent = await Dues.MonthlyOnly(_context.Payments)
            .Where(p => p.ForMonth == forMonth && p.Student!.SchoolClassId == schoolClassId)
            .GroupBy(p => p.StudentId)
            .Select(g => new { StudentId = g.Key, Paid = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.StudentId, x => x.Paid);
```

in `Receipt`:

```csharp
        var paidForMonth = await Dues.MonthlyOnly(_context.Payments)
            .Where(p => p.StudentId == payment.StudentId && p.ForMonth == payment.ForMonth)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;
```

in `OutstandingAsync`:

```csharp
        var paid = await Dues.MonthlyOnly(_context.Payments)
            .Where(p => p.StudentId == studentId && p.ForMonth == forMonth)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        return Dues.Outstanding(fee, paid);
```

`Areas/Student/Controllers/HomeController.cs`, in `Index`:

```csharp
        var paidThisMonth = await Dues.MonthlyOnly(_context.Payments)
            .Where(p => p.StudentId == me.Id && p.ForMonth == thisMonth)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        ViewBag.Fee = fee;
        ViewBag.PaidThisMonth = paidThisMonth;
        ViewBag.DueThisMonth = Dues.Outstanding(fee, paidThisMonth);
```

- [ ] **Step 5: Add to the selftest runner**

In `Program.cs`:

```csharp
if (args.Contains("selftest"))
{
    GradingSelfTest.Run();
    PhoneSelfTest.Run();
    DuesSelfTest.Run();
    return 0;
}
```

- [ ] **Step 6: Run to verify it passes**

```bash
dotnet run -- selftest
```

Expected: four lines, all passing.

- [ ] **Step 7: Confirm no unfiltered sum is left**

```bash
grep -rn "SumAsync(p => (decimal?)p.Amount)" --include=*.cs . | grep -v "^./bin\|^./obj"
```

Expected: three results, each on a line immediately following a `Dues.MonthlyOnly(` call. `Payments/Student.cs` lists a history rather than computing a balance and correctly shows every kind.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "Count only monthly fees towards a month's balance

Four screens summed a month's payments to work out what was owed. An admission
fee recorded in the same month would have told a guardian their tuition was
nearly settled when none of it had been paid. One filter, four call sites."
```

---

### Task 8: Normalize the guardian phone numbers on record

**Files:**
- Modify: `Areas/Admin/Controllers/StudentsController.cs`
- Create: `Migrations/<timestamp>_NormalizeGuardianPhones.cs`

**Interfaces:**
- Consumes: `PhoneNumber.Normalize` from Task 1.
- Produces: every `Students.GuardianPhone` in canonical 11-digit form.

- [ ] **Step 1: Normalize on save**

In `StudentsController`, in both `Create` and `Edit`, immediately before the `ModelState.IsValid` check:

```csharp
        // The guardian's number becomes their portal password, so what is stored and what
        // they type at sign-in have to be the same string.
        student.GuardianPhone = PhoneNumber.Normalize(student.GuardianPhone) ?? student.GuardianPhone;
        student.Phone = PhoneNumber.Normalize(student.Phone) ?? student.Phone;
        student.AlternatePhone = PhoneNumber.Normalize(student.AlternatePhone) ?? student.AlternatePhone;
```

Falling back to the original rather than null keeps a number the office deliberately typed in some other form — a landline, say — instead of silently erasing it. `Edit` must also copy `existingStudent.AlternatePhone = student.AlternatePhone;`.

Add `using TSC.Data;` if it is not already there.

- [ ] **Step 2: Backfill the existing rows**

```bash
dotnet ef migrations add NormalizeGuardianPhones
```

The scaffolded migration will be empty — no model changed. Put this in `Up`:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Strip everything that is not a digit, drop a 880 country code, and keep the result
    // only when it is a real 11-digit mobile. Anything else is left exactly as typed.
    migrationBuilder.Sql(@"
        UPDATE ""Students""
        SET ""GuardianPhone"" = sub.normalized
        FROM (
            SELECT ""Id"",
                   CASE
                     WHEN length(regexp_replace(""GuardianPhone"", '\D', '', 'g')) = 13
                          AND regexp_replace(""GuardianPhone"", '\D', '', 'g') LIKE '880%'
                       THEN '0' || substring(regexp_replace(""GuardianPhone"", '\D', '', 'g') from 4)
                     ELSE regexp_replace(""GuardianPhone"", '\D', '', 'g')
                   END AS normalized
            FROM ""Students""
            WHERE ""GuardianPhone"" IS NOT NULL
        ) AS sub
        WHERE ""Students"".""Id"" = sub.""Id""
          AND length(sub.normalized) = 11
          AND sub.normalized LIKE '01%'
          AND ""Students"".""GuardianPhone"" IS DISTINCT FROM sub.normalized;");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // Not reversible: the original formatting is not recorded anywhere.
}
```

- [ ] **Step 3: Apply and verify**

```bash
dotnet ef database update
psql -h localhost -U postgres -d coaching_center -c \
  'SELECT count(*) AS not_canonical FROM "Students"
   WHERE "GuardianPhone" IS NOT NULL AND "GuardianPhone" !~ ''^01[0-9]{9}$'';'
```

Expected: `0`, or a small count you can inspect by eye — those are numbers that were never mobiles.

- [ ] **Step 4: Full check**

```bash
dotnet build -t:Compile --nologo -v q
dotnet run -- selftest
```

Expected: build succeeds, four selftest lines pass.

- [ ] **Step 5: Commit and push**

```bash
git add -A
git commit -m "Normalize guardian phone numbers to 01XXXXXXXXX

The guardian's number becomes their portal password in a later phase, so the
stored form and the typed form have to agree. Existing rows are backfilled;
anything that is not a mobile number is left as the office typed it."
git push origin main
```

---

## Done when

- `dotnet build -t:Compile` succeeds with no warnings.
- `dotnet run -- selftest` prints four passing lines.
- `grep -rn "\bBatch\b\|BatchId\|BatchSubject\|ClassLevel" --include=*.cs --include=*.cshtml .` returns nothing outside `Migrations/`.
- `SELECT count(*) FROM "Students"` is 87, and the mark, attendance and payment counts match the pre-migration backup.
- Every `SchoolClasses` row has a `Level` of 2, 3, 4 or 5, all distinct.
- The app runs and every page renders as it did before — this phase changes no behaviour a user can see, except that the class dropdown no longer has a redundant twin.
