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

    // POST: /Admin/Students/ResetLogin/5  -- issue a fresh password for an existing login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetLogin(int id)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id);

        if (student?.UserId == null)
            return NotFound();

        var (user, password, error) = await LoginProvisioning.ResetPasswordAsync(_users, student.UserId);

        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["NewLogin"] = $"{user!.Email}|{password}";
        TempData["Success"] = $"New password issued for {student.FullName}.";

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

    // GET: /Admin/Students?q=&schoolClassId=&classLevel=
    public async Task<IActionResult> Index(bool archived = false, string? q = null,
        int? schoolClassId = null, string? classLevel = null)
    {
        var query = _context.Students.AsQueryable();

        if (archived)
            query = query.IgnoreQueryFilters().Where(s => !s.IsActive);

        var search = q?.Trim();

        if (!string.IsNullOrEmpty(search))
        {
            // ILIKE, so a name typed in either script and any case still finds the student.
            var pattern = $"%{search}%";

            query = query.Where(s =>
                EF.Functions.ILike(s.FullName, pattern) ||
                EF.Functions.ILike(s.StudentId, pattern) ||
                (s.Phone != null && EF.Functions.ILike(s.Phone, pattern)) ||
                (s.GuardianName != null && EF.Functions.ILike(s.GuardianName, pattern)) ||
                (s.GuardianPhone != null && EF.Functions.ILike(s.GuardianPhone, pattern)));
        }

        if (schoolClassId is > 0)
            query = query.Where(s => s.SchoolClassId == schoolClassId);

        if (!string.IsNullOrWhiteSpace(classLevel))
            query = query.Where(s => s.ClassLevel == classLevel);

        ViewBag.Archived = archived;
        ViewBag.ArchivedCount = await _context.Students
            .IgnoreQueryFilters()
            .CountAsync(s => !s.IsActive);

        ViewBag.Query = search;
        ViewBag.SchoolClassId = schoolClassId;
        ViewBag.ClassLevel = classLevel;
        ViewBag.SchoolClasses = await _context.SchoolClasses.OrderBy(b => b.Name).ToListAsync();
        ViewBag.ClassLevels = await _context.Students
            .Where(s => s.ClassLevel != null)
            .Select(s => s.ClassLevel!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return View(await query
            .Include(s => s.SchoolClass)
            .OrderBy(s => s.StudentId)
            .ToListAsync());
    }

    // GET: /Admin/Students/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var student = await _context.Students
            .Include(s => s.SchoolClass)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
            return NotFound();

        return View(student);
    }

    // GET: /Admin/Students/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadSchoolClasses();

        return View();
    }

    // POST: /Admin/Students/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Student student)
    {
        // IgnoreQueryFilters: an archived student still holds their Student ID, and the unique
        // index does not care that they are archived.
        if (await _context.Students.IgnoreQueryFilters().AnyAsync(s => s.StudentId == student.StudentId))
            ModelState.AddModelError(nameof(student.StudentId), "That Student ID is already taken.");

        if (!ModelState.IsValid)
        {
            await LoadSchoolClasses();
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

        await LoadSchoolClasses();

        return View(student);
    }

    // POST: /Admin/Students/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Student student)
    {
        if (id != student.Id)
            return NotFound();

        if (await _context.Students.IgnoreQueryFilters().AnyAsync(s => s.StudentId == student.StudentId && s.Id != id))
            ModelState.AddModelError(nameof(student.StudentId), "That Student ID is already taken.");

        if (!ModelState.IsValid)
        {
            await LoadSchoolClasses();
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
        existingStudent.SchoolClassId = student.SchoolClassId;

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
            .Include(s => s.SchoolClass)
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

        // Marks, attendance and payments all cascade from Students. Deleting a student with
        // any history would silently destroy their whole record, including the fee ledger,
        // so that is only allowed for a record added by mistake. Everyone else gets archived.
        if (await HasHistoryAsync(id))
        {
            TempData["Error"] =
                $"{student.FullName} has marks, attendance or payments recorded. " +
                "Archive them instead — deleting would destroy that history.";

            return RedirectToAction(nameof(Delete), new { id });
        }

        await DeleteLoginAsync(student);

        _context.Students.Remove(student);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Student deleted.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /Admin/Students/Archive/5  -- the student left; keep their record, drop their access
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
            return NotFound();

        // An archived student must not still be able to sign in and read their portal.
        await DeleteLoginAsync(student);

        student.IsActive = false;
        await _context.SaveChangesAsync();

        TempData["Success"] =
            $"{student.FullName} archived. Their marks, attendance and payments are kept.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /Admin/Students/Restore/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var student = await _context.Students
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
            return NotFound();

        student.IsActive = true;
        await _context.SaveChangesAsync();

        TempData["Success"] = $"{student.FullName} restored. Create a login if they need one.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> HasHistoryAsync(int studentId) =>
        await _context.Marks.AnyAsync(m => m.StudentId == studentId)
        || await _context.Attendances.AnyAsync(a => a.StudentId == studentId)
        || await _context.Payments.AnyAsync(p => p.StudentId == studentId);

    // Removing the student record must take the login with it, or an orphaned account is
    // left behind that can still sign in.
    private async Task DeleteLoginAsync(Student student)
    {
        if (student.UserId == null)
            return;

        var user = await _users.FindByIdAsync(student.UserId);

        if (user != null)
            await _users.DeleteAsync(user);

        student.UserId = null;
    }

    private async Task LoadSchoolClasses()
    {
        ViewBag.SchoolClasses = await _context.SchoolClasses
            .OrderBy(b => b.Name)
            .ToListAsync();
    }
}