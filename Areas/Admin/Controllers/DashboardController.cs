using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TSC.Data;

namespace TSC.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.StudentCount = await _context.Students.CountAsync();
        ViewBag.TeacherCount = await _context.Teachers.CountAsync();
        ViewBag.SchoolClassCount = await _context.SchoolClasses.CountAsync();
        ViewBag.SubjectCount = await _context.Subjects.CountAsync();

        // Today in Dhaka, not on the server's clock -- a UTC host rolls over to a new date at
        // 6 AM here, exactly when the morning batch sits down.
        var today = Clock.Today;
        var month = Clock.ThisMonth;

        ViewBag.Today = today;
        ViewBag.PresentToday = await _context.Attendances
            .CountAsync(a => a.Date == today && a.IsPresent);

        ViewBag.MarkedToday = await _context.Attendances.CountAsync(a => a.Date == today);

        // Voided receipts are filtered out globally, so this is money actually in the box.
        ViewBag.CollectedThisMonth = await _context.Payments
            .Where(p => p.ForMonth == month)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        ViewBag.ThisMonth = month;

        return View();
    }
}
