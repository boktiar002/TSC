using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TSC.Data;
using TSC.Models;

namespace TSC.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class StudentsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _users;

    public StudentsController(ApplicationDbContext context, UserManager<ApplicationUser> users)
    {
        _context = context;
        _users = users;
    }

    // POST: /Admin/Students/CreateLogin/5  -- give this student a portal account
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLogin(int id, string? email)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
            return NotFound();

        if (student.UserId != null)
        {
            TempData["Error"] = $"{student.FullName} already has a login.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var address = string.IsNullOrWhiteSpace(email) ? student.Email : email.Trim();

        var (user, password, error) = await LoginProvisioning.CreateAsync(
            _users, address ?? "", student.FullName, "Student");

        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Details), new { id });
        }

        student.UserId = user!.Id;
        student.Email ??= address;
        await _context.SaveChangesAsync();

        // Shown once and never recoverable: the admin reads it out, then it is gone.
        TempData["NewLogin"] = $"{address}|{password}";
        TempData["Success"] = $"Login created for {student.FullName}.";

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Admin/Students/RemoveLogin/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLogin(int id)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id);

        if (student?.UserId == null)
            return NotFound();

        var user = await _users.FindByIdAsync(student.UserId);

        if (user != null)
            await _users.DeleteAsync(user);

        student.UserId = null;
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Login removed for {student.FullName}.";

        return RedirectToAction(nameof(Details), new { id });
    }

    // GET: /Admin/Students
    public async Task<IActionResult> Index()
    {
        var students = await _context.Students
            .Include(s => s.Batch)
            .OrderBy(s => s.FullName)
            .ToListAsync();

        return View(students);
    }

    // GET: /Admin/Students/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var student = await _context.Students
            .Include(s => s.Batch)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
            return NotFound();

        return View(student);
    }

    // GET: /Admin/Students/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadBatches();

        return View();
    }

    // POST: /Admin/Students/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Student student)
    {
        if (await _context.Students.AnyAsync(s => s.StudentId == student.StudentId))
            ModelState.AddModelError(nameof(student.StudentId), "That Student ID is already taken.");

        if (!ModelState.IsValid)
        {
            await LoadBatches();
            return View(student);
        }

        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Student added successfully.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Admin/Students/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
            return NotFound();

        await LoadBatches();

        return View(student);
    }

    // POST: /Admin/Students/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Student student)
    {
        if (id != student.Id)
            return NotFound();

        if (await _context.Students.AnyAsync(s => s.StudentId == student.StudentId && s.Id != id))
            ModelState.AddModelError(nameof(student.StudentId), "That Student ID is already taken.");

        if (!ModelState.IsValid)
        {
            await LoadBatches();
            return View(student);
        }

        var existingStudent = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == id);

        if (existingStudent == null)
            return NotFound();

        existingStudent.StudentId = student.StudentId;
        existingStudent.FullName = student.FullName;
        existingStudent.ClassLevel = student.ClassLevel;
        existingStudent.Phone = student.Phone;
        existingStudent.Email = student.Email;
        existingStudent.Address = student.Address;
        existingStudent.GuardianName = student.GuardianName;
        existingStudent.GuardianPhone = student.GuardianPhone;
        existingStudent.DateOfBirth = student.DateOfBirth;
        existingStudent.BatchId = student.BatchId;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Student updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Admin/Students/Delete/5
    [HttpGet]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var student = await _context.Students
            .Include(s => s.Batch)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
            return NotFound();

        return View(student);
    }

    // POST: /Admin/Students/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
            return NotFound();

        _context.Students.Remove(student);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Student deleted successfully.";

        return RedirectToAction(nameof(Index));
    }

    private async Task LoadBatches()
    {
        ViewBag.Batches = await _context.Batches
            .OrderBy(b => b.Name)
            .ToListAsync();
    }
}