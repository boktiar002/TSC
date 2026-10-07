# TSC — Coaching Centre Management

An ASP.NET Core 10 (MVC + Razor Pages) application for running a coaching centre end to end:
student records, classes, teachers, subjects, attendance, exams and marks, fees, and notices —
with separate portals for admins, teachers, and students. Built on PostgreSQL via EF Core and
deployed as a single Docker container.

## Features

- **Students** — admission, editing, search, and archiving (an archived student's history is
  kept, never deleted).
- **Classes & subjects** — one of four class levels (2–5), each with its own subjects, monthly
  fee, and roster. A student's shift (Morning/Evening) is tracked separately from their class —
  both sit the same paper and are ranked together.
- **Teachers** — records and portal access, with mark entry restricted to the subjects actually
  assigned to them.
- **Attendance** — daily roll call, a follow-up view for students with a worrying absence
  pattern, and a full per-student history.
- **Exams & marks** — exam creation, mark entry, results, per-student progress over time, and a
  printable report card.
- **Fees** — payment entry and receipts. A mistaken payment is voided rather than deleted, so the
  cash record stays intact and auditable.
- **Notices** — centre-wide announcements, visible to every portal.
- **Portal logins** — an admin issues each student's and teacher's login; nobody self-registers.
  The login ID is the Student/Teacher ID already printed on their record (most students have no
  email), and a generated password is shown once, built to be read aloud and typed without
  mixing up `0`/`O` or `1`/`l`.

## Tech stack

- ASP.NET Core 10 (MVC + Razor Pages for Identity)
- PostgreSQL 18 via EF Core / Npgsql
- ASP.NET Core Identity (cookie auth), with the key ring persisted in Postgres so logins survive
  a container restart
- QuestPDF for report cards and receipts
- Docker for deployment (tested on Render)

## Getting started

**Prerequisites:** .NET 10 SDK, PostgreSQL 18, `dotnet-ef` (`dotnet tool install -g dotnet-ef`).

The connection string is **not** in `appsettings.json` — it carries the database password.
Set it locally with user-secrets:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=coaching_center;Username=postgres;Password=<yours>"
```

On a server, set the environment variable instead (a host that links a database automatically,
like Render, sets `DATABASE_URL` in URL form, which the app normalizes itself):

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

| Login ID | Password |
|---|---|
| `admin@tsc.local` | `Admin@12345` |

This seeding is development-only — it would otherwise hand a published credential to anyone who
has seen this repo. On a server, create the first admin explicitly:

```bash
dotnet TSC.dll create-admin <email> <password> [full name]
```

or set `BOOTSTRAP_ADMIN_EMAIL` / `BOOTSTRAP_ADMIN_PASSWORD` before the first deploy. See
`Data/DbSeeder.cs`.

## Roles

Signing in drops you at the right place automatically (`/` routes by role).

- **Admin** — full management: students, classes, teachers, subjects, attendance, exams and
  marks, fees, notices. Also issues portal logins for students and teachers.
- **Teacher** — own classes and rosters, and mark entry restricted to the subjects actually
  assigned to them.
- **Student** — own results, attendance, and fee history.

Students and teachers do not self-register. An admin creates their login from the student's
detail page or the teacher list; the generated password is shown once and is not recoverable
(only re-issuable).

## Deployment

The app ships as a Docker container (see `Dockerfile`) and reads its port from `PORT`
(`ASPNETCORE_URLS` is set from it at container start). It assumes a reverse proxy terminates TLS
in front of it — forwarded headers are configured so secure cookies, HTTPS URLs, and HSTS work
correctly behind one.

- `GET /health` — unauthenticated, no database — just confirms the process is up, for
  uptime/keep-warm monitors.
- Migrations run automatically on startup (`Database.MigrateAsync()`), so there is no separate
  migration step on deploy.
- The Data Protection key ring is stored in Postgres (`DataProtectionKeys` table), not on local
  disk, so auth cookies stay valid across restarts and multiple instances.

## Tests

A few small pieces of real logic have runnable self-checks instead of a test project:

```bash
dotnet run -- selftest
```

Covers grading, phone-number parsing, the Asia/Dhaka "today" clock, and connection-string
normalization.

## Project layout

```
Areas/Admin       management UI (Authorize: Admin)
Areas/Student     student portal (Authorize: Student)   namespace StudentPortal
Areas/Teacher     teacher portal (Authorize: Teacher)   namespace TeacherPortal
Data/             DbContext, seeding, login provisioning, mark-entry helpers
Models/           entities + the grading scale
Migrations/       EF Core migrations
```

The portal namespaces are `StudentPortal`/`TeacherPortal` rather than matching their folders,
because `TSC.Areas.Student` would shadow the `Student` model type inside the admin controllers.
Routing is driven by the `[Area]` attribute, so URLs are unaffected.

## Notes on the data model

- **Class vs. batch** — a *class* is one of the four grade levels the centre teaches (2–5);
  exams, subjects, fees, ranking, and attendance are all scoped per class. A *batch* is just
  which shift a student attends (Morning/Evening) — both shifts sit the same paper and are
  ranked together, so it is a property on `Student`, not a separate manageable entity.
- **Dates** — anything that is a calendar date (date of birth, attendance, exam date, fee month)
  is `DateOnly` mapped to PostgreSQL `date`. Only genuine instants (`Notice.PublishedAt`,
  `Payment.VoidedAt`) use `DateTime` with `timestamptz`, and those are always stored in UTC —
  Npgsql rejects anything else.
- **Soft deletion** — archived students and voided payments are never deleted. Both are hidden
  from normal queries by an EF query filter and can still be reached with
  `IgnoreQueryFilters()` where the history matters (an archive list, a duplicate-ID check).
