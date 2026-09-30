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
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
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
        payment.ForMonth = FirstOfMonth(payment.ForMonth);

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
    public async Task<IActionResult> Student(int id)
    {
        var student = await _context.Students
            .Include(s => s.SchoolClass)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
            return NotFound();

        ViewBag.Student = student;

        return View(await _context.Payments
            .Where(p => p.StudentId == id)
            .OrderByDescending(p => p.ForMonth)
            .ThenByDescending(p => p.PaymentDate)
            .ToListAsync());
    }

    // GET: /Admin/Payments/Receipt/5  -- the slip the guardian takes home
    public async Task<IActionResult> Receipt(int id)
    {
        var payment = await _context.Payments
            .Include(p => p.Student).ThenInclude(s => s!.SchoolClass)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment?.Student == null)
            return NotFound();

        // Everything received for that month, not just this slip, so the balance is honest
        // even when the fee was handed over in instalments.
        var paidForMonth = await _context.Payments
            .Where(p => p.StudentId == payment.StudentId && p.ForMonth == payment.ForMonth)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        ViewBag.PaidForMonth = paidForMonth;
        ViewBag.MonthlyFee = payment.Student.SchoolClass?.MonthlyFee ?? 0m;

        return View(payment);
    }

    // POST: /Admin/Payments/Delete/5  -- for a mis-keyed receipt
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
            return NotFound();

        _context.Payments.Remove(payment);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Payment record deleted.";

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

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    // <input type="month"> posts "2026-09"; our own links pass "2026-09-01". Accept both,
    // and never let a junk query string throw — fall back to the current month.
    private static DateOnly ParseMonth(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length == 7 ? value + "-01" : value;

        return DateOnly.TryParse(text, CultureInfo.InvariantCulture, out var parsed)
            ? FirstOfMonth(parsed)
            : FirstOfMonth(DateOnly.FromDateTime(DateTime.Today));
    }
}
