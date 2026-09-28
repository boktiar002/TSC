# TSC — Coaching Centre Management

ASP.NET Core 10 MVC + PostgreSQL (EF Core) app for running a coaching centre: students,
batches, teachers, subjects, attendance, exams and marks, fees, and notices — with separate
portals for admin, teachers, and students.

## Running it

**Prerequisites:** .NET 10 SDK, PostgreSQL 18, `dotnet-ef` (`dotnet tool install -g dotnet-ef`).

The connection string is **not** in `appsettings.json` — it carries the database password.
Set it locally with user-secrets:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=coaching_center;Username=postgres;Password=<yours>"
```

On a server, use the environment variable instead:

```bash
ConnectionStrings__DefaultConnection="Host=...;Database=...;Username=...;Password=..."
```

Then:

```bash
createdb coaching_center      # first time only
dotnet ef database update
dotnet run
```

The app starts on <http://localhost:5231>.

## First sign-in

In **Development**, startup seeds the three roles and one admin account:

| Email | Password |
|---|---|
| `admin@tsc.local` | `Admin@12345` |

This seeding is development-only. On a server, create the first admin explicitly — see
`Data/DbSeeder.cs`.

## Roles

Signing in drops you at the right place automatically (`/` routes by role).

- **Admin** — full management: students, batches, teachers, subjects, attendance, exams and
  marks, fees, notices. Also creates portal logins for students and teachers.
- **Teacher** — own batches and rosters, and mark entry restricted to the subjects actually
  assigned to them.
- **Student** — own results, attendance, and fee history.

Students and teachers do not self-register. An admin creates their login from the student's
detail page or the teacher list; the generated password is shown once and is not recoverable.

## Tests

Grading is the one bit of real arithmetic in the app and has a runnable check:

```bash
dotnet run -- selftest
```

## Layout

```
Areas/Admin       management UI (Authorize: Admin)
Areas/Student     student portal (Authorize: Student)   namespace StudentPortal
Areas/Teacher     teacher portal (Authorize: Teacher)   namespace TeacherPortal
Data/             DbContext, seeding, shared mark-entry and login provisioning
Models/           entities + the grading scale
Migrations/       EF Core migrations
```

The portal namespaces are `StudentPortal`/`TeacherPortal` rather than matching their folders,
because `TSC.Areas.Student` would shadow the `Student` model type inside the admin
controllers. Routing is driven by the `[Area]` attribute, so URLs are unaffected.

## Dates

Anything that is a calendar date (date of birth, attendance, exam date, fee month) is
`DateOnly` mapped to PostgreSQL `date`. Only genuine instants (`Notice.PublishedAt`) use
`DateTime` with `timestamptz`, and those must be UTC — Npgsql rejects anything else.
