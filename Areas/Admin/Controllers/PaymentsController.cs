using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TSC.Data;
using TSC.Models;

namespace TSC.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class PaymentsController : Controller
{
    private static readonly string[] Methods = { "Cash", "bKash", "Nagad", "Rocket", "Bank Transfer" };

    private readonly ApplicationDbContext _context;

    public PaymentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Admin/Payments?schoolClassId=1&month=2026-09  -- who owes what this month
    public async Task<IActionResult> Index(int? schoolClassId, string? month)
    {
        var forMonth = ParseMonth(month);

        ViewBag.SchoolClasses = await _context.SchoolClasses.OrderBy(b => b.Name).ToListAsync();
        ViewBag.SchoolClassId = schoolClassId;
        ViewBag.Month = forMonth;

        if (schoolClassId == null)
            return View(new List<Student>());

        var schoolClass = await _context.SchoolClasses.FirstOrDefaultAsync(b => b.Id == schoolClassId);

        if (schoolClass == null)
            return NotFound();

        var roster = await _context.Students
            .Where(s => s.SchoolClassId == schoolClassId)
            .OrderBy(s => s.FullName)
            .ToListAsync();

        // Partial payments are normal, so sum rather than take the first row.
        ViewBag.PaidByStudent = await _context.Payments
            .Where(p => p.ForMonth == forMonth && p.Student!.SchoolClassId == schoolClassId)
            .GroupBy(p => p.StudentId)
            .Select(g => new { StudentId = g.Key, Paid = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.StudentId, x => x.Paid);

        ViewBag.SchoolClass = schoolClass;

        return View(roster);
    }

    // GET: /Admin/Payments/Create?studentId=5&month=2026-09
    [HttpGet]
    public async Task<IActionResult> Create(int? studentId, string? month)
    {
        var forMonth = ParseMonth(month);

        await LoadFormOptions();

        var payment = new Payment
        {
            StudentId = studentId ?? 0,
            ForMonth = forMonth,
            PaymentDate = Clock.Today,
            PaymentMethod = Methods[0]
        };

        // Prefill with whatever is still outstanding for that month.
        if (studentId != null)
            payment.Amount = await OutstandingAsync(studentId.Value, forMonth);

        return View(payment);
    }

    // POST: /Admin/Payments/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Payment payment)
    {
        payment.ForMonth = Clock.FirstOf(payment.ForMonth);

        if (!await _context.Students.AnyAsync(s => s.Id == payment.StudentId))
            ModelState.AddModelError(nameof(payment.StudentId), "Please select a student.");

        if (!ModelState.IsValid)
        {
            await LoadFormOptions();
            return View(payment);
        }

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        var student = await _context.Students.FirstAsync(s => s.Id == payment.StudentId);

        TempData["Success"] =
            $"Recorded {payment.Amount:0.##} for {student.FullName} ({payment.ForMonth:MMMM yyyy}).";

        return RedirectToAction(nameof(Index), new
        {
            schoolClassId = student.SchoolClassId,
            month = payment.ForMonth.ToString("yyyy-MM")
        });
    }

    // GET: /Admin/Payments/Student/5  -- one student's payment history
    // Archived students included: their fee ledger is exactly what the archive is for.
    public async Task<IActionResult> Student(int id)
    {
        var student = await _context.Students
            .IgnoreQueryFilters()
            .Include(s => s.SchoolClass)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
            return NotFound();

        ViewBag.Student = student;

        // Voided rows are listed here, struck through, so the ledger stays reconcilable.
        return View(await _context.Payments
            .IgnoreQueryFilters()
            .Where(p => p.StudentId == id)
            .OrderByDescending(p => p.ForMonth)
            .ThenByDescending(p => p.PaymentDate)
            .ToListAsync());
    }

    // GET: /Admin/Payments/Receipt/5  -- the slip the guardian takes home
    public async Task<IActionResult> Receipt(int id)
    {
        var payment = await _context.Payments
            .IgnoreQueryFilters()
            .Include(p => p.Student).ThenInclude(s => s!.SchoolClass)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment?.Student == null)
            return NotFound();

        // Everything received for that month, not just this slip, so the balance is honest
        // even when the fee was handed over in instalments.
        var paidForMonth = await _context.Payments
            // Archived only: a voided receipt must not count towards the month's total.
            .IgnoreQueryFilters(["Archived"])
            .Where(p => p.StudentId == payment.StudentId && p.ForMonth == payment.ForMonth)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        ViewBag.PaidForMonth = paidForMonth;
        ViewBag.MonthlyFee = payment.Student.SchoolClass?.MonthlyFee ?? 0m;

        return View(payment);
    }

    // POST: /Admin/Payments/Void/5  -- for a mis-keyed receipt
    // Voided, not deleted: the guardian may be holding the paper copy, and a ledger that can
    // lose rows cannot be reconciled against the cash box. The row stays, out of every total,
    // with the admin who voided it recorded against it.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(int id)
    {
        // Ignore both filters: voiding an already-voided row is a no-op, not a 404, and an
        // archived student's mis-keyed receipt still needs voiding.
        var payment = await _context.Payments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
            return NotFound();

        if (payment.IsVoided)
            TempData["Error"] = "That receipt is already voided.";
        else
        {
            payment.IsVoided = true;
            payment.VoidedAt = DateTime.UtcNow;
            payment.VoidedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Receipt No. {payment.Id:D5} voided. It no longer counts towards any total.";
        }

        return RedirectToAction(nameof(Student), new { id = payment.StudentId });
    }

    private async Task<decimal> OutstandingAsync(int studentId, DateOnly forMonth)
    {
        var fee = await _context.Students
            .Where(s => s.Id == studentId)
            .Select(s => s.SchoolClass!.MonthlyFee)
            .FirstOrDefaultAsync();

        var paid = await _context.Payments
            .Where(p => p.StudentId == studentId && p.ForMonth == forMonth)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        return Math.Max(0, fee - paid);
    }

    private async Task LoadFormOptions()
    {
        ViewBag.Methods = Methods;

        ViewBag.Students = await _context.Students
            .Include(s => s.SchoolClass)
            .OrderBy(s => s.FullName)
            .ToListAsync();
    }


    // <input type="month"> posts "2026-09"; our own links pass "2026-09-01". Accept both,
    // and never let a junk query string throw — fall back to the current month.
    private static DateOnly ParseMonth(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length == 7 ? value + "-01" : value;

        return DateOnly.TryParse(text, CultureInfo.InvariantCulture, out var parsed)
            ? Clock.FirstOf(parsed)
            : Clock.ThisMonth;
    }
}
