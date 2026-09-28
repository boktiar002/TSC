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

    // GET: /Admin/Attendance?batchId=1&date=2026-09-28  -- the daily sheet
    public async Task<IActionResult> Index(int? batchId, DateOnly? date)
    {
        var day = date ?? DateOnly.FromDateTime(DateTime.Today);

        ViewBag.Batches = await _context.Batches.OrderBy(b => b.Name).ToListAsync();
        ViewBag.BatchId = batchId;
        ViewBag.Date = day;

        if (batchId == null)
            return View(new List<Student>());

        var roster = await _context.Students
            .Where(s => s.BatchId == batchId)
            .OrderBy(s => s.FullName)
            .ToListAsync();

        // Absent by default only on a fresh sheet; an existing sheet shows what was recorded.
        var recorded = await _context.Attendances
            .Where(a => a.Date == day && roster.Select(s => s.Id).Contains(a.StudentId))
            .ToDictionaryAsync(a => a.StudentId, a => a.IsPresent);

        ViewBag.Recorded = recorded;
        ViewBag.AlreadyTaken = recorded.Count > 0;

        return View(roster);
    }

    // POST: /Admin/Attendance
    // `present` carries only the ticked students; the roster is re-read from the DB so a
    // tampered form cannot mark someone in another batch.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(int batchId, DateOnly date, int[] present)
    {
        var roster = await _context.Students
            .Where(s => s.BatchId == batchId)
            .Select(s => s.Id)
            .ToListAsync();

        if (roster.Count == 0)
        {
            TempData["Error"] = "That batch has no students yet.";
            return RedirectToAction(nameof(Index), new { batchId, date = date.ToString("yyyy-MM-dd") });
        }

        var existing = await _context.Attendances
            .Where(a => a.Date == date && roster.Contains(a.StudentId))
            .ToDictionaryAsync(a => a.StudentId);

        foreach (var studentId in roster)
        {
            var isPresent = present.Contains(studentId);

            if (existing.TryGetValue(studentId, out var row))
                row.IsPresent = isPresent;
            else
                _context.Attendances.Add(new Attendance
                {
                    StudentId = studentId,
                    Date = date,
                    IsPresent = isPresent
                });
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Attendance saved for {date:dd MMM yyyy} — {present.Length} of {roster.Count} present.";

        return RedirectToAction(nameof(Index), new { batchId, date = date.ToString("yyyy-MM-dd") });
    }

    // GET: /Admin/Attendance/Student/5  -- one student's record
    public async Task<IActionResult> Student(int id)
    {
        var student = await _context.Students
            .Include(s => s.Batch)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
            return NotFound();

        ViewBag.Student = student;

        return View(await _context.Attendances
            .Where(a => a.StudentId == id)
            .OrderByDescending(a => a.Date)
            .ToListAsync());
    }
}
