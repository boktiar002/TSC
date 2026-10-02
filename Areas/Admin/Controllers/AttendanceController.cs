using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TSC.Data;
using TSC.Models;

namespace TSC.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AttendanceController : Controller
{
    private readonly ApplicationDbContext _context;

    public AttendanceController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Admin/Attendance?schoolClassId=1&date=2026-09-28  -- the daily sheet
    public async Task<IActionResult> Index(int? schoolClassId, DateOnly? date)
    {
        var day = date ?? Clock.Today;

        ViewBag.SchoolClasses = await _context.SchoolClasses.OrderBy(b => b.Name).ToListAsync();
        ViewBag.SchoolClassId = schoolClassId;
        ViewBag.Date = day;

        if (schoolClassId == null)
            return View((RollCall?)null);

        ViewBag.RollCallTitle = await _context.SchoolClasses
            .Where(b => b.Id == schoolClassId)
            .Select(b => b.Name)
            .FirstOrDefaultAsync();

        return View(await RollCallBook.LoadAsync(_context, schoolClassId.Value, day));
    }

    // POST: /Admin/Attendance/Toggle
    // The roll call: one tap marks the student present, tapping again takes it back. There is
    // no separate save — each tap is the save, because the phone doing this walks around a
    // classroom and may never come back to the page.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int studentId, int schoolClassId, DateOnly date)
    {
        var tap = await RollCallBook.ToggleAsync(_context, schoolClassId, studentId, date);

        if (tap == null)
            return NotFound();

        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            return Json(new { present = tap.Value.Present, presentCount = tap.Value.PresentCount, total = tap.Value.Total });

        return RedirectToAction(nameof(Index), new { schoolClassId, date = date.ToString("yyyy-MM-dd") });
    }

    // GET: /Admin/Attendance/Followup?schoolClassId=1&days=30&threshold=75
    // The call list: who has been slipping, so the office rings the guardian before a term's
    // worth of absence turns into a student who quietly stopped coming.
    public async Task<IActionResult> Followup(int? schoolClassId, int days = 30, int threshold = 75)
    {
        days = Math.Clamp(days, 7, 365);
        threshold = Math.Clamp(threshold, 1, 100);

        var today = Clock.Today;
        var from = today.AddDays(-days);

        ViewBag.SchoolClasses = await _context.SchoolClasses.OrderBy(b => b.Name).ToListAsync();
        ViewBag.SchoolClassId = schoolClassId;
        ViewBag.Days = days;
        ViewBag.Threshold = threshold;
        ViewBag.From = from;

        var students = await _context.Students
            .Include(s => s.SchoolClass)
            .Where(s => schoolClassId == null || s.SchoolClassId == schoolClassId)
            .ToListAsync();

        var studentIds = students.Select(s => s.Id).ToList();

        var records = await _context.Attendances
            .Where(a => a.Date >= from && studentIds.Contains(a.StudentId))
            .Select(a => new { a.StudentId, a.Date, a.IsPresent })
            .ToListAsync();

        var byStudent = records.GroupBy(a => a.StudentId).ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<FollowupRow>();

        foreach (var student in students)
        {
            if (!byStudent.TryGetValue(student.Id, out var own) || own.Count == 0)
                continue; // never marked in this window: nothing to judge them on

            var present = own.Count(a => a.IsPresent);
            var rate = Math.Round(present * 100m / own.Count, 1);

            if (rate >= threshold)
                continue;

            // How many of the most recent marked days in a row they have missed.
            var streak = 0;

            foreach (var day in own.OrderByDescending(a => a.Date))
            {
                if (day.IsPresent)
                    break;

                streak++;
            }

            rows.Add(new FollowupRow
            {
                Student = student,
                DaysRecorded = own.Count,
                DaysPresent = present,
                Rate = rate,
                MissedInARow = streak,
                LastPresent = own.Where(a => a.IsPresent).Select(a => (DateOnly?)a.Date).Max(),
            });
        }

        ViewBag.Reviewed = byStudent.Count;

        return View(rows
            .OrderByDescending(r => r.MissedInARow)
            .ThenBy(r => r.Rate)
            .ToList());
    }

    // GET: /Admin/Attendance/Student/5  -- one student's record
    // Archived students included, same as their fee history.
    public async Task<IActionResult> Student(int id)
    {
        var student = await _context.Students
            .IgnoreQueryFilters()
            .Include(s => s.SchoolClass)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
            return NotFound();

        ViewBag.Student = student;

        return View(await _context.Attendances
            .IgnoreQueryFilters()
            .Where(a => a.StudentId == id)
            .OrderByDescending(a => a.Date)
            .ToListAsync());
    }
}

// One line of the follow-up call list.
public class FollowupRow
{
    public Student Student { get; init; } = null!;
    public int DaysRecorded { get; init; }
    public int DaysPresent { get; init; }
    public decimal Rate { get; init; }
    public int MissedInARow { get; init; }
    public DateOnly? LastPresent { get; init; }
}
