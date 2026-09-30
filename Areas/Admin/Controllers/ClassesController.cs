using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TSC.Data;
using TSC.Models;

namespace TSC.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ClassesController : Controller
{
    private readonly ApplicationDbContext _context;

    public ClassesController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var schoolClasses = await _context.SchoolClasses
            .Include(b => b.Students)
            .Include(b => b.ClassSubjects)
            .OrderBy(b => b.Name)
            .ToListAsync();

        return View(schoolClasses);
    }

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SchoolClass schoolClass)
    {
        if (await _context.SchoolClasses.AnyAsync(b => b.Name == schoolClass.Name))
            ModelState.AddModelError(nameof(schoolClass.Name), "A class with that name already exists.");

        if (!ModelState.IsValid)
            return View(schoolClass);

        _context.SchoolClasses.Add(schoolClass);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Class created successfully.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var schoolClass = await _context.SchoolClasses.FirstOrDefaultAsync(b => b.Id == id);

        if (schoolClass == null)
            return NotFound();

        return View(schoolClass);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SchoolClass schoolClass)
    {
        if (id != schoolClass.Id)
            return NotFound();

        if (await _context.SchoolClasses.AnyAsync(b => b.Name == schoolClass.Name && b.Id != id))
            ModelState.AddModelError(nameof(schoolClass.Name), "A class with that name already exists.");

        if (!ModelState.IsValid)
            return View(schoolClass);

        var existing = await _context.SchoolClasses.FirstOrDefaultAsync(b => b.Id == id);

        if (existing == null)
            return NotFound();

        existing.Name = schoolClass.Name;
        existing.Description = schoolClass.Description;
        existing.MonthlyFee = schoolClass.MonthlyFee;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Class updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var schoolClass = await _context.SchoolClasses
            .Include(b => b.Students)
            .Include(b => b.ClassSubjects)
                .ThenInclude(bs => bs.Subject)
            .Include(b => b.ClassSubjects)
                .ThenInclude(bs => bs.Teacher)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (schoolClass == null)
            return NotFound();

        await LoadAssignmentOptions(schoolClass);

        return View(schoolClass);
    }

    // POST: /Admin/Classes/AssignSubject/5  -- assign a subject + its teacher to this class
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignSubject(int id, int subjectId, int teacherId)
    {
        if (!await _context.SchoolClasses.AnyAsync(b => b.Id == id))
            return NotFound();

        if (await _context.ClassSubjects.AnyAsync(bs => bs.SchoolClassId == id && bs.SubjectId == subjectId))
            TempData["Error"] = "That subject is already assigned to this class.";
        else
        {
            _context.ClassSubjects.Add(new ClassSubject
            {
                SchoolClassId = id,
                SubjectId = subjectId,
                TeacherId = teacherId
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Subject assigned to class.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Admin/Classes/RemoveSubject/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSubject(int id, int classSubjectId)
    {
        var assignment = await _context.ClassSubjects
            .FirstOrDefaultAsync(bs => bs.Id == classSubjectId && bs.SchoolClassId == id);

        if (assignment == null)
            return NotFound();

        _context.ClassSubjects.Remove(assignment);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Assignment removed.";

        return RedirectToAction(nameof(Details), new { id });
    }

    // Only offer subjects not already on this class.
    private async Task LoadAssignmentOptions(SchoolClass schoolClass)
    {
        var taken = schoolClass.ClassSubjects.Select(bs => bs.SubjectId).ToList();

        ViewBag.AvailableSubjects = await _context.Subjects
            .Where(s => !taken.Contains(s.Id))
            .OrderBy(s => s.Name)
            .ToListAsync();

        ViewBag.Teachers = await _context.Teachers
            .OrderBy(t => t.FullName)
            .ToListAsync();
    }
}
