# TSC — professional overhaul design

**Date:** 2026-10-01
**Status:** awaiting review

## Why

The system works but reads as a student project: a 1320px-capped Bootstrap layout in
default admin navy, an admission form that asks the office to invent a Student ID, a
guardian portal that shows fewer facts than the data behind it, and a schema whose
vocabulary is inverted from the centre's own.

The goal is a system a coaching centre would be glad to run its office from, operated
almost entirely from a phone.

## Who uses it, and on what

| Who | Device | Job |
|---|---|---|
| Office staff | Phone, occasionally a PC | Admissions, fees, notices, everything |
| Teachers | Phone | Roll call, mark entry, and now admissions |
| Guardians | Android phone, metered data | Check one child's attendance, marks, dues |

The guardian is the portal's real user. Students are 7–11 years old (classes 2–5); they
are not reading a fee ledger. Every guardian-facing decision follows from that.

## Decisions

| Area | Decision |
|---|---|
| Schema | `Batch`→`SchoolClass` (+`Level`); `Student.Batch` = Morning/Afternoon; `ClassLevel` dropped |
| Admission | Single screen, one transaction, ID generated on save, teacher or admin, phone-first |
| Credentials | Username = Student ID, password = guardian phone, admin-visible |
| Guardian portal | Separate mobile layout, Bangla labels, English numerals, rank + grade |
| Visual | Bottle-green register, marking-pen red, marigold merit, Anek, mobile-first |
| Fees | One fee per class; morning and afternoon do not differ |

---

## Section 1 — Data model

### `Student`

Added: `PhotoPath` `string?(200)`, `Gender` `string?(10)`, `BloodGroup` `string?(5)`,
`PreviousSchool` `string?(120)`, `GuardianRelation` `string?(30)`,
`GuardianOccupation` `string?(60)`, `AlternatePhone` `string?(20)`,
`AdmissionDate` `DateOnly` (required, defaults today),
`Batch` `string?(10)` — `"Morning"` / `"Afternoon"`.

Removed: `ClassLevel`. It duplicated `SchoolClass.Name` with nothing keeping the two in
sync, so a student could sit in `Class 5` with `ClassLevel = "3"` and no validation
objected. The class now has exactly one source of truth.

`SessionYear` is a computed property, `=> AdmissionDate.Year`, not a column. It becomes a
real column the day a student is admitted in December *for* the following session.

`Student.Batch` is nullable because the 87 existing students have no shift on record.
Rosters render `—`. The admission form requires it for new students.

### `SchoolClass` (renamed from `Batch`)

Added: `Level` `int` — 2, 3, 4, 5. ID generation and sorting read an integer rather than
parsing `"Class 3"` out of a name. `MonthlyFee` stays here: one fee per class.

Exactly four rows. Morning and afternoon are not separate classes — the centre's own
import notes say the two branches "sit the same paper and are ranked together", so exams,
subjects, fees, ranking and attendance are all per class.

### `Payment`

Added: `Kind` `string(20)`, default `"Monthly"`, values `Monthly` / `Admission`.
Without it an admission fee would silently cancel a student's monthly balance, because
`PaymentsController.Index` sums payments by month to compute dues. The dues calculation
filters to `Kind == "Monthly"`.

Added: `RecordedByUserId` `string?`. Teachers can now write payment rows, so the ledger
records who took the money. This is the control that makes teacher fee collection
acceptable; without it, "every teacher can write to the fee ledger" has no accounting.

### Migrations

**`ClassesAndBatches`** — rename `Batches`→`SchoolClasses`, `BatchSubjects`→`ClassSubjects`,
and every `BatchId`→`SchoolClassId` (EF `RenameTable`/`RenameColumn` preserve data); add
`SchoolClasses.Level`; backfill it from the name; add `Students.Batch`; drop
`Students.ClassLevel`.

**`ProfessionalIntake`** — the new `Student` columns, `Payment.Kind`,
`Payment.RecordedByUserId`, `ApplicationUser.IsPortalAccount`.

Order matters: backfill `Level` before dropping `ClassLevel`.

---

## Section 2 — Classes, batches, and Student ID

### Naming

`Batch` → `SchoolClass`, `BatchSubject` → `ClassSubject`, `*.BatchId` → `SchoolClassId`.
159 references across 50 files. The view sweep in Section 6 rewrites most of those files
anyway, so the view half of the rename is free if done in the same pass and costs double
afterwards.

`Student.Batch` is then free for Morning/Afternoon, and code matches the centre's
vocabulary exactly: `student.SchoolClass` is Class 3, `student.Batch` is Morning.

Two batch values as `private static readonly string[] Batches = { "Morning", "Afternoon" }`,
following `PaymentsController.Methods`. No enum, no lookup table, no CRUD screen for two
values that will not change.

### Student ID generation

Format `TSC` + 2-digit level + 2-digit serial → `TSC0317`, continuing the convention
established in `Data/imports/renumber_student_ids.sql`.

- **Not a form field.** The office never types or sees it before saving. Generated
  server-side at POST from the chosen class; first shown on the admission slip.
- Serial is **per class, shared across both batches** — morning and afternoon number into
  one sequence, matching how the centre ranks them.
- Counts archived students via `IgnoreQueryFilters()`. A departed student's number is
  never reused; their marks and fee rows still reference them.
- Concurrency: two simultaneous admissions compute the same serial. The unique index on
  `StudentId` rejects the second, so nothing corrupts. Catch `DbUpdateException`,
  recompute, retry once.
  `ponytail: retry-once suits a front desk with one or two clerks; a real sequence
  generator only if it ever collides twice.`
- `ponytail: 2-digit serial caps at 99 per class — widen the format when a class passes 99.`

### Attendance

Teachers already pick by class — `MyBatches(me)` returns `Class 2`…`Class 5`. The picker
is mislabelled, not miswired; it becomes "Class".

One sheet per class listing every student, morning and afternoon together. No shift
filter.

---

## Section 3 — Credentials

Username = `StudentId`. Password = normalized `GuardianPhone`.

The admin can always tell a guardian their password because there is no stored secret to
retrieve — the password *is* a field already visible on the student's record. This removes
the need for the `TempData["NewLogin"]` "write this down now" banner (~45 lines of
`_Layout.cshtml`) and `LoginProvisioning.GeneratePassword()` entirely.

**Security position.** `TSC0317` is enumerable and an 11-digit phone number is weak and
socially known. Accepted for the guardian portal, which is read-only and scoped to one
child's own record. Explicitly **not** applied to Admin or Teacher accounts, which see
every student in the centre.

- `GuardianPhone` normalized to 11 digits on save, so "your password is your phone number"
  is unambiguous when the office typed `01712-345678`.
- No guardian phone on the record → no login can be created. Explicit error.
- `ApplicationUser.IsPortalAccount` plus a ~20-line `IPasswordValidator<ApplicationUser>`:
  digits-only allowed for portal accounts, full strength for everyone else. Relaxing
  `options.Password` globally would weaken admin passwords too. The role cannot be used —
  `LoginProvisioning` adds the role *after* `CreateAsync`, so the validator cannot see it.
- `_LoginPartial.cshtml` hides "Change password" for the Student role. If a guardian
  changes it, the admin can no longer tell them what it is and the scheme breaks.
- `options.Lockout` enabled: 5 attempts, 15 minutes. The login page already passes
  `lockoutOnFailure: true` but `options.Lockout` is unconfigured. With a guessable
  username this is the control that carries the scheme. Not optional.
- Admin student detail gains a "Portal login" card: username, password, copy buttons, and
  a "Reset password to phone number" button.

**Login page** — scaffold `Areas/Identity/Pages/Account/Login` (nothing is scaffolded
today). Show/hide toggle on the password field (~8 lines, no dependency).
`inputmode="numeric"` so an Android keyboard opens on digits. Field relabelled "Student ID
or email" — one page serves guardians and admins, since the default template already
passes that value to `PasswordSignInAsync` as a username.

---

## Section 4 — Admission

Root `AdmissionsController` at `/Admissions/New`, `[Authorize(Roles = "Admin,Teacher")]`.
Not in the Admin area — `/Admin/...` is wrong in a teacher's URL bar — and not bolted onto
`StudentsController`, which is already ~380 lines of CRUD, archiving and login
provisioning.

Teachers get **full admission including fees**, and the class dropdown lists all four
classes. That widening applies to this form only; everywhere else teachers stay locked to
`MyBatches(me)` with `Forbid()` as now. `Payment.RecordedByUserId` records who collected.

One view model, `AdmissionForm` — the project's first, justified because the form writes
to three places (`Students`, `Payments`, the photo file) and cannot bind to an entity.

**One screen, five titled sections**, tab order straight through: Student · Guardian ·
Class & batch · Photo · Fees. No wizard: a front-desk worker is talking to a guardian who
corrects themselves mid-sentence, and one scrollable screen beats a step that hides the
previous answer.

**Photo** — `<input type="file" accept="image/*" capture="environment">` opens the phone
camera directly. Native, no library.

Phone cameras write 3–12 MB JPEGs. Client-side canvas downscale to ~800px before upload
(~25 lines of vanilla JS) saves the teacher's mobile data and keeps the guardian portal
light. Server limit 4 MB as the backstop.

Server-side validation is the trust boundary and is not simplified: content type in
`image/jpeg|png|webp`, size ≤4 MB, **and a magic-byte header check** — the extension is
attacker-controlled, the file header is not. The filename and extension are written by us
from the validated type, so nothing user-supplied reaches the path. Stored at
`wwwroot/uploads/students/{Id}.{ext}`, gitignored.

**Fee amounts.** The first-month fee prefills from `SchoolClass.MonthlyFee`. The admission
fee has no home in the schema and is not getting one — it is typed on the form, because at
a centre this size it is negotiated per family as often as not. Both are editable, both may
be left at zero. Add a configured admission fee only if it turns out to be fixed in
practice.

**One transaction:** save student → get `Id` → write photo → set `PhotoPath` → add up to
two `Payment` rows → `SaveChanges` → commit. A failed photo write rolls the transaction
back; the orphaned file is harmless and is overwritten on retry.
`ponytail: no orphan-file cleanup — one file per failed admission in a gitignored folder.`

**Then `/Admissions/Slip/{id}`** — one printable sheet reusing the existing `tsc-sheet`
and `no-print` styles from `Payments/Receipt.cshtml`: masthead, photo, student and guardian
details, ID card block, fee lines received, and the login credentials. The whole admission
goes home on paper in one print.

---

## Section 5 — Guardian portal

New `_GuardianLayout.cshtml`, not the admin shell. Mobile-first, Anek Bangla, four large
tap targets, a persistent `tel:` call button. Bangla labels, English numerals, ৳ on every
amount.

**হোম — one screen that answers everything:**

- Child's photo, name, class and batch.
- **Attention band, rendered only when true**: money owed, or an absence in the last seven
  days. A calm screen means nothing is wrong.
- **Attendance as a count, then the missed dates listed.** "19 of 22 days in September",
  then the three dates. The dates are the actionable part — that is what a parent asks the
  child about. `92%` is unactionable.
- **Latest result as rank and grade** — position out of class size, and the GPA-5 letter
  from `Grading.For()`. `Ranking.RankAsync` returns the whole class including every
  `Student` object; the view takes **only this child's `Rank` and the row count**. Other
  children's names are never rendered — privacy, and it starts fights between parents.
- Latest notice.

**ফলাফল** — one card per exam: rank, grade, then subject marks stacked. The current
6-column matrix is unusable at 360px. **উপস্থিতি** — lists *absences*, not 180 present
days. **ফি** — month by month, paid or due, receipt link per payment. **নোটিশ** — as now.

**Tone.** Facts, not verdicts. Absences get dates and neutral wording, no red "POOR" badge;
dues get one calm amber band, not alarm red. Shame makes a parent stop opening the app.

Currently unused by this area and being put to work: `Data/Ranking.cs`, `Models/Grading.cs`,
`Exams/ReportCard.cshtml`. The guardian today sees strictly less than the data supports.

---

## Section 6 — Design system

### Grounding

The authority object of a coaching centre is the ruled register. The repo says so:
`Data/imports/class3_weekly_2026.sql` transcribes "সাপ্তাহিক ফলাফল (তৃতীয় শ্রেণী)", a
hand-ruled weekly merit sheet; `RollCallBook.cs` is a register; `Payments/Receipt.cshtml`
is a numbered receipt book. Digitalizing the era means the register becomes the interface,
not that it is replaced by a grid of stat cards.

A first pass — warm cream, serif display, terracotta accent — was discarded as the generic
house style of AI-generated design, unrelated to a Bangladeshi coaching centre.

### Tokens

| Token | Hex | Source |
|---|---|---|
| `--ink-green` | `#0A3A2A` | Register cover, school board, institutional signage. Chrome. |
| `--green-700` | `#15563F` | Hover and gradient partner. |
| `--mark-red` | `#CE1B2B` | The teacher's marking pen. Means *needs attention* only. |
| `--marigold` | `#F0A22E` | গাঁদা, the prize-giving colour. Achievement only. Fill behind dark text, never text on white. |
| `--paper` | `#F6F8F5` | Register paper under a green cover. Deliberately not cream. |
| `--ink` | `#12211C` | Body text: a green-black from the brand, not a generic tinted `#111`. |

Navy and blue are removed — that palette is the default admin template and is why the
system reads as generic.

**Type:** `Anek Bangla` + `Anek Latin`, one variable superfamily covering both scripts, so
the Bangla portal and the English admin share a design. Replaces Inter.
`font-variant-numeric: tabular-nums` on data tables.
*Risk:* Anek's Latin is untested at 13–14px in dense tables; fallback is Hanken Grotesk for
Latin with Anek Bangla retained for the portal. Decide on a real screen.

### Shell — mobile-first

- **Phones:** bottom tab bar, four destinations plus "More". Under the thumb, not behind a
  hamburger. "More" opens Bootstrap's `offcanvas`, already in the bundled JS. The four are
  per role — Admin: Today, Students, Attendance, Fees. Teacher: Today, Attendance, Marks,
  Admit. Guardian's four are its own layout, in Section 5.
- **`lg` and up:** fixed sidebar, content full-bleed to 1760px with fluid gutters.
- `Views/Shared/_Layout.cshtml` wraps navbar, `<main>` and footer in `.container`, which
  hard-caps at **1320px** — 600px of dead margin on a 1920px monitor while mark sheets are
  squeezed into the middle. The cap comes off.
- Ruled tables stack into cards below `md`; the register rule survives as the row divider.
  `_ProgressTable` (12 columns) gets horizontal scroll with a sticky name column rather
  than a restructure. Mark entry is already 3 columns and needs no change.
- Tap targets ≥44px.

### Motion — two moments, both answering a tap

1. **Roll call ink-fill** — tapping a row in `_RollCall.cshtml` floods it with ink-green
   from the tap point. The daily ritual of the system, and a confirmation that must be
   unmistakable at arm's length.
2. **Admission slip assembly** — masthead, then photo, then fee lines, once, at the moment
   something real happened.

Nothing else animates. Fade-and-slide-up on every card and a hover lift on every tile is
the generated-page default. `prefers-reduced-motion` respected on both.

### Imagery

No stock photography — it is the cheapest signal available, and using real children's
photos as marketing chrome is a consent problem. Imagery is student photos on rosters,
detail pages and the printed ID card; and a landing hero that is a register page rendered
in CSS with the centre's own live numbers in its ruled rows.

### Tells already in the code, removed in the sweep

- `.tsc-eyebrow` — a tracked-out ALL-CAPS label above a heading.
- Middle-dot meta strings — `StudentId · Batch · Class …` on most pages.
- `shadow-sm` and one border-radius on every card regardless of hierarchy. The admin
  dashboard's four identical count cards are the worst instance and go first.

### Weight budget

The layout pulls Google Fonts and the full bootstrap-icons webfont (~180KB) from CDNs for
roughly 30 icons, over guardians' metered mobile data. Replaced with inline SVG for the
icons actually used.

---

## Section 7 — Features

All read from tables that already exist:

1. **Dues across all classes.** `PaymentsController.Index` returns an empty list until a
   class is chosen, so "who owes money this month" cannot be answered in one look today.
2. **Global student search in the top bar.** `StudentsController.Index` already does
   `ILIKE` across name, ID, phone and guardian; the bar points at it.
3. **A dashboard of decisions, not counts.** Which classes have no attendance taken today,
   who is absent, dues outstanding, exams this week. Replaces four count cards.
4. **Merit certificates for the top three.** `Ranking.RankAsync` computes placings and
   `tsc-sheet` prints; prize-giving is a real ritual and parents keep these.
5. **Printable student ID card** with photo and credentials.

---

## Phasing

Each phase is independently deployable.

1. **Schema and naming** — the two migrations, the `SchoolClass` rename, `Student.Batch`.
   No visual change. Ships first because everything else builds on the names.
2. **Design system and shell** — tokens, mobile shell, `.container` cap removed, the view
   sweep, the tells removed.
3. **Credentials** — validator, lockout, login page scaffold, admin login card. Must
   precede admission: the slip prints the login, so the scheme has to exist first.
4. **Admission** — `AdmissionsController`, the form, photo pipeline, ID generation, slip.
5. **Guardian portal** — layout, the five Bangla screens.
6. **Features** — the five above.

## Testing

`dotnet run -- selftest` is the existing pattern — assert-based, no framework. Extended for
the logic that can silently produce wrong output:

- **Student ID generation**: next serial per class; archived students counted; collision
  retry; the 99 ceiling.
- **Phone normalization**: `+8801712345678`, `01712-345678`, `01712 345678` all → `01712345678`.
- **Dues with `Kind`**: an admission payment must not reduce a monthly balance.
- **Photo validation**: a `.jpg` whose header is not an image is rejected.
- **Guardian rank projection**: the payload for a guardian screen contains no other
  student's name.

The last one is a privacy assertion, not a UI test, and is the one most worth having.

## Out of scope

- **SMS notices to guardians.** Highest-value item not included; needs a paid gateway and
  phone verification. Spec separately.
- **CSV/Excel export.** Add when asked.
- **Sibling switching.** Username = Student ID means one login per child, so a guardian
  with two children has two logins. `Student.UserId` stays unique and no ownership
  plumbing is needed. Add if a parent complains.
- **Bulk "set batch" screen** for the 87 existing students with no shift recorded.
- **Changing a guardian's username** when their number changes — currently Remove login →
  Create login.
- **Bangla admin UI.** Admin and Teacher stay English; they are trained staff.
