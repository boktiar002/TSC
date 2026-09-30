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

    // POST: /Admin/Teachers/CreateLogin/5  -- give this teacher a portal account
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLogin(int id, string? email)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == id);

        if (teacher == null)
            return NotFound();

        if (teacher.UserId != null)
        {
            TempData["Error"] = $"{teacher.FullName} already has a login.";
            return RedirectToAction(nameof(Index));
        }

        var address = string.IsNullOrWhiteSpace(email) ? teacher.Email : email.Trim();

        var (user, password, error) = await LoginProvisioning.CreateAsync(
            _users, address ?? "", teacher.FullName, "Teacher");

        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Index));
        }

        teacher.UserId = user!.Id;
        teacher.Email ??= address;
        await _context.SaveChangesAsync();

        // Shown once and never recoverable: the admin reads it out, then it is gone.
        TempData["NewLogin"] = $"{address}|{password}";
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

        TempData["NewLogin"] = $"{user!.Email}|{password}";
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
            .Include(t => t.BatchSubjects).ThenInclude(bs => bs.Batch)
            .Include(t => t.BatchSubjects).ThenInclude(bs => bs.Subject)
            .OrderBy(t => t.FullName)
            .ToListAsync();

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

        // Cascade would silently drop the batch assignments; make the admin unassign first.
        if (await _context.BatchSubjects.AnyAsync(bs => bs.TeacherId == id))
            TempData["Error"] = $"{teacher.FullName} is still assigned to a batch. Remove those assignments first.";
        else
        {
            // Take the login with the record, or an orphaned account is left behind that
            // can still sign in.
            if (teacher.UserId != null)
            {
                var user = await _users.FindByIdAsync(teacher.UserId);

                if (user != null)
                    await _users.DeleteAsync(user);
            }

            _context.Teachers.Remove(teacher);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Teacher deleted successfully.";
        }

        return RedirectToAction(nameof(Index));
    }
}
