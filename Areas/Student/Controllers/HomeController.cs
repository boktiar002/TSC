using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TSC.Data;
using TSC.Models;

namespace TSC.Areas.StudentPortal.Controllers;

// Everything here is scoped to the signed-in student's own record. No action takes an id
// from the URL, so there is nothing to tamper with to read someone else's results.
[Area("Student")]
[Authorize(Roles = "Student")]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var me = await MeAsync();

        if (me == null)
            return View("NotLinked");

        var today = DateOnly.FromDateTime(DateTime.Today);
        var thisMonth = new DateOnly(today.Year, today.Month, 1);

        var attendance = await _context.Attendances
            .Where(a => a.StudentId == me.Id)
            .ToListAsync();

        ViewBag.AttendanceRate = attendance.Count == 0
            ? (int?)null
            : attendance.Count(a => a.IsPresent) * 100 / attendance.Count;

        ViewBag.DaysRecorded = attendance.Count;

        var fee = me.Batch?.MonthlyFee ?? 0m;

        var paidThisMonth = await _context.Payments
            .Where(p => p.StudentId == me.Id && p.ForMonth == thisMonth)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        ViewBag.Fee = fee;
        ViewBag.PaidThisMonth = paidThisMonth;
        ViewBag.DueThisMonth = Math.Max(0, fee - paidThisMonth);
        ViewBag.ThisMonth = thisMonth;

        ViewBag.ExamCount = await _context.Marks
            .Where(m => m.StudentId == me.Id)
            .Select(m => m.ExamId)
            .Distinct()
            .CountAsync();

        ViewBag.Notices = await ActiveNoticesAsync();

        return View(me);
    }

    public async Task<IActionResult> Results()
    {
        var me = await MeAsync();

        if (me == null)
            return View("NotLinked");

        // Only exams this student actually has marks in; an exam they missed entirely
        // would otherwise show as a row of zeroes.
        var examIds = await _context.Marks
            .Where(m => m.StudentId == me.Id)
            .Select(m => m.ExamId)
            .Distinct()
            .ToListAsync();

        ViewBag.Student = me;

        ViewBag.Exams = await _context.Exams
            .Where(e => examIds.Contains(e.Id))
            .Include(e => e.ExamSubjects).ThenInclude(es => es.Subject)
            .OrderByDescending(e => e.ExamDate)
            .ToListAsync();

        ViewBag.Scores = await _context.Marks
            .Where(m => m.StudentId == me.Id)
            .ToDictionaryAsync(m => (m.ExamId, m.SubjectId), m => m.Score);

        return View();
    }

    public async Task<IActionResult> Attendance()
    {
        var me = await MeAsync();

        if (me == null)
            return View("NotLinked");

        ViewBag.Student = me;

        return View(await _context.Attendances
            .Where(a => a.StudentId == me.Id)
            .OrderByDescending(a => a.Date)
            .ToListAsync());
    }

    public async Task<IActionResult> Fees()
    {
        var me = await MeAsync();

        if (me == null)
            return View("NotLinked");

        ViewBag.Student = me;

        return View(await _context.Payments
            .Where(p => p.StudentId == me.Id)
            .OrderByDescending(p => p.ForMonth)
            .ThenByDescending(p => p.PaymentDate)
            .ToListAsync());
    }

    public async Task<IActionResult> Notices() => View(await ActiveNoticesAsync());

    private Task<Student?> MeAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return _context.Students
            .Include(s => s.Batch)
            .FirstOrDefaultAsync(s => s.UserId == userId);
    }

    private Task<List<Notice>> ActiveNoticesAsync() =>
        _context.Notices
            .Where(n => n.IsActive)
            .OrderByDescending(n => n.PublishedAt)
            .ToListAsync();
}
