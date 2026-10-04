using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TSC.Data;
using TSC.Models;

namespace TSC.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class TeachersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _users;

    public TeachersController(ApplicationDbContext context, UserManager<ApplicationUser> users)
    {
        _context = context;
        _users = users;
    }

    // POST: /Admin/Teachers/MakeAdmin/5
    //
    // The person running the centre is also one of its teachers, and the centre cannot be left
    // with a single key holder -- somebody has to be able to take the fees and call the roll on
    // the day that one person is away. So admin is a role granted on top of a teacher account,
    // not a separate kind of user, and more than one teacher can hold it.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MakeAdmin(int id)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == id);

        if (teacher == null)
            return NotFound();

        if (teacher.UserId == null)
        {
            TempData["Error"] =
                $"{teacher.FullName} needs a portal login first — create one, then make them an admin.";

            return RedirectToAction(nameof(Index));
        }

        var user = await _users.FindByIdAsync(teacher.UserId);

        if (user == null)
            return NotFound();

        if (await _users.IsInRoleAsync(user, DbSeeder.AdminRole))
        {
            TempData["Error"] = $"{teacher.FullName} is already an admin.";
            return RedirectToAction(nameof(Index));
        }

        var granted = await _users.AddToRoleAsync(user, DbSeeder.AdminRole);

        if (!granted.Succeeded)
        {
            TempData["Error"] = string.Join(" ", granted.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] =
            $"{teacher.FullName} is now an admin. They keep their teaching pages and gain the " +
            "whole office — students, fees, exams, notices and logins.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /Admin/Teachers/RemoveAdmin/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAdmin(int id)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == id);

        if (teacher?.UserId == null)
            return NotFound();

        var user = await _users.FindByIdAsync(teacher.UserId);

        if (user == null)
            return NotFound();

        if (!await _users.IsInRoleAsync(user, DbSeeder.AdminRole))
        {
            TempData["Error"] = $"{teacher.FullName} is not an admin.";
            return RedirectToAction(nameof(Index));
        }

        if (await IsLastAdminAsync(user))
        {
            TempData["Error"] =
                $"{teacher.FullName} is the only admin left. Make someone else an admin first, " +
                "or nobody can get into the office again.";

            return RedirectToAction(nameof(Index));
        }

        var removed = await _users.RemoveFromRoleAsync(user, DbSeeder.AdminRole);

        if (!removed.Succeeded)
        {
            TempData["Error"] = string.Join(" ", removed.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = $"{teacher.FullName} is no longer an admin. They keep their teaching pages.";

        return RedirectToAction(nameof(Index));
    }

    // Locking everyone out of the office is not recoverable from inside the app -- it would take
    // `dotnet run -- create-admin` on the server -- so every path that could drop the last admin
    // checks this first.
    private async Task<bool> IsLastAdminAsync(ApplicationUser user)
    {
        if (!await _users.IsInRoleAsync(user, DbSeeder.AdminRole))
            return false;

        var admins = await _users.GetUsersInRoleAsync(DbSeeder.AdminRole);

        return admins.Count(a => a.Id != user.Id) == 0;
    }

    // POST: /Admin/Teachers/CreateLogin/5  -- give this teacher a portal account
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLogin(int id)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == id);

        if (teacher == null)
            return NotFound();

        if (teacher.UserId != null)
        {
            TempData["Error"] = $"{teacher.FullName} already has a login.";
            return RedirectToAction(nameof(Index));
        }

        // The Teacher ID is the login ID, matching how student logins are made.
        var (user, password, error) = await LoginProvisioning.CreateAsync(
            _users, teacher.TeacherId, teacher.FullName, "Teacher", teacher.Email);

        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Index));
        }

        teacher.UserId = user!.Id;
        await _context.SaveChangesAsync();

        TempData["NewLogin"] = $"{teacher.TeacherId}|{password}";
        TempData["Success"] = $"Login created for {teacher.FullName}.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /Admin/Teachers/ResetLogin/5  -- issue a fresh password for an existing login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetLogin(int id)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == id);

        if (teacher?.UserId == null)
            return NotFound();

        var (user, password, error) = await LoginProvisioning.ResetPasswordAsync(_users, teacher.UserId);

        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Index));
        }

        TempData["NewLogin"] = $"{user!.UserName}|{password}";
        TempData["Success"] = $"New password issued for {teacher.FullName}.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /Admin/Teachers/RemoveLogin/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLogin(int id)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == id);

        if (teacher?.UserId == null)
            return NotFound();

        var user = await _users.FindByIdAsync(teacher.UserId);

        // Deleting the account takes the admin role with it, so this is the same lockout as
        // RemoveAdmin by another route.
        if (user != null && await IsLastAdminAsync(user))
        {
            TempData["Error"] =
                $"{teacher.FullName} is the only admin left. Removing their login would lock " +
                "everyone out of the office. Make someone else an admin first.";

            return RedirectToAction(nameof(Index));
        }

        if (user != null)
            await _users.DeleteAsync(user);

        teacher.UserId = null;
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Login removed for {teacher.FullName}.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Admin/Teachers
    public async Task<IActionResult> Index()
    {
        var teachers = await _context.Teachers
            .Include(t => t.ClassSubjects).ThenInclude(bs => bs.SchoolClass)
            .Include(t => t.ClassSubjects).ThenInclude(bs => bs.Subject)
            .OrderBy(t => t.FullName)
            .ToListAsync();

        // Which of them hold the admin role, so the list can show it and offer the toggle.
        // One lookup for the page rather than one per teacher.
        var adminIds = (await _users.GetUsersInRoleAsync(DbSeeder.AdminRole))
            .Select(u => u.Id)
            .ToHashSet();

        ViewBag.AdminUserIds = adminIds;
        ViewBag.AdminCount = adminIds.Count;
        ViewBag.MyUserId = _users.GetUserId(User);

        return View(teachers);
    }

    // GET: /Admin/Teachers/Create
    [HttpGet]
    public IActionResult Create() => View();

    // POST: /Admin/Teachers/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Teacher teacher)
    {
        if (await _context.Teachers.AnyAsync(t => t.TeacherId == teacher.TeacherId))
            ModelState.AddModelError(nameof(teacher.TeacherId), "That Teacher ID is already taken.");

        if (!ModelState.IsValid)
            return View(teacher);

        _context.Teachers.Add(teacher);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Teacher added successfully.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Admin/Teachers/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == id);

        if (teacher == null)
            return NotFound();

        return View(teacher);
    }

    // POST: /Admin/Teachers/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Teacher teacher)
    {
        if (id != teacher.Id)
            return NotFound();

        if (await _context.Teachers.AnyAsync(t => t.TeacherId == teacher.TeacherId && t.Id != id))
            ModelState.AddModelError(nameof(teacher.TeacherId), "That Teacher ID is already taken.");

        if (!ModelState.IsValid)
            return View(teacher);

        var existing = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == id);

        if (existing == null)
            return NotFound();

        existing.TeacherId = teacher.TeacherId;
        existing.FullName = teacher.FullName;
        existing.Phone = teacher.Phone;
        existing.Email = teacher.Email;
        existing.Specialization = teacher.Specialization;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Teacher updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /Admin/Teachers/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == id);

        if (teacher == null)
            return NotFound();

        // Cascade would silently drop the class assignments; make the admin unassign first.
        if (await _context.ClassSubjects.AnyAsync(bs => bs.TeacherId == id))
            TempData["Error"] = $"{teacher.FullName} is still assigned to a class. Remove those assignments first.";
        else
        {
            // Take the login with the record, or an orphaned account is left behind that
            // can still sign in.
            var user = teacher.UserId == null ? null : await _users.FindByIdAsync(teacher.UserId);

            if (user != null && await IsLastAdminAsync(user))
            {
                TempData["Error"] =
                    $"{teacher.FullName} is the only admin left. Deleting them would lock " +
                    "everyone out of the office. Make someone else an admin first.";

                return RedirectToAction(nameof(Index));
            }

            if (user != null)
                await _users.DeleteAsync(user);

            _context.Teachers.Remove(teacher);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Teacher deleted successfully.";
        }

        return RedirectToAction(nameof(Index));
    }
}
