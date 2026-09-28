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
        ViewBag.BatchCount = await _context.Batches.CountAsync();
        ViewBag.SubjectCount = await _context.Subjects.CountAsync();

        return View();
    }
}
