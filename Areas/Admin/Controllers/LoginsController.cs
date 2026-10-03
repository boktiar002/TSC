using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TSC.Data;
using TSC.Models;

namespace TSC.Areas.Admin.Controllers;

// One screen holding every portal credential the centre has issued, because the question the
// office actually gets is "what is this child's password?" months after the account was made --
// not "make them a new one", which logs the student out of the account they were already using.
//
// Admin accounts are deliberately absent: their passwords are never stored in the clear.
[Area("Admin")]
[Authorize(Roles = "Admin")]
public class LoginsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _users;

    public LoginsController(ApplicationDbContext context, UserManager<ApplicationUser> users)
    {
        _context = context;
        _users = users;
    }

    public record Row(
        string Name,
        string Role,
        string LoginId,
        string? Password,
        int? StudentId,
        int? TeacherId,
        string? ClassName);

    // GET: /Admin/Logins?q=&role=
    public async Task<IActionResult> Index(string? q = null, string? role = null)
    {
        // IgnoreQueryFilters: an archived student has no login, but a student archived while
        // this page is open should still be explainable rather than silently vanishing.
        var students = await _context.Students
            .IgnoreQueryFilters()
            .Include(s => s.SchoolClass)
            .Where(s => s.UserId != null)
            .OrderBy(s => s.StudentId)
            .ToListAsync();

        var teachers = await _context.Teachers
            .Where(t => t.UserId != null)
            .OrderBy(t => t.TeacherId)
            .ToListAsync();

        // One lookup for every account on the page rather than one per row.
        var userIds = students.Select(s => s.UserId!)
            .Concat(teachers.Select(t => t.UserId!))
            .ToList();

        var accounts = await _users.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var rows = students
            .Select(s => new Row(
                s.FullName, "Student",
                accounts.TryGetValue(s.UserId!, out var su) ? su.UserName ?? s.StudentId : s.StudentId,
                accounts.TryGetValue(s.UserId!, out var sp) ? sp.IssuedPassword : null,
                s.Id, null, s.SchoolClass?.Name))
            .Concat(teachers.Select(t => new Row(
                t.FullName, "Teacher",
                accounts.TryGetValue(t.UserId!, out var tu) ? tu.UserName ?? t.TeacherId : t.TeacherId,
                accounts.TryGetValue(t.UserId!, out var tp) ? tp.IssuedPassword : null,
                null, t.Id, t.Specialization)))
            .ToList();

        // Counted before filtering, so the role tabs keep showing the real totals rather than
        // the total of whatever tab is already selected.
        ViewBag.StudentCount = rows.Count(r => r.Role == "Student");
        ViewBag.TeacherCount = rows.Count(r => r.Role == "Teacher");

        if (!string.IsNullOrWhiteSpace(role))
            rows = rows.Where(r => r.Role == role).ToList();

        var search = q?.Trim();

        if (!string.IsNullOrEmpty(search))
        {
            rows = rows.Where(r =>
                r.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.LoginId.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        ViewBag.Query = search;
        ViewBag.Role = role;

        return View(rows);
    }
}
