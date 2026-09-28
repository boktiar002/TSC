using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TSC.Data;
using TSC.Models;

namespace TSC.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class SubjectsController : Controller
{
    private readonly ApplicationDbContext _context;

    public SubjectsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Admin/Subjects  -- list doubles as the add form; a subject is only name + code.
    public async Task<IActionResult> Index()
    {
        ViewBag.Subjects = await _context.Subjects
            .Include(s => s.BatchSubjects)
            .OrderBy(s => s.Name)
            .ToListAsync();

        return View(new Subject());
    }

    // POST: /Admin/Subjects/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Subject subject)
    {
        if (await _context.Subjects.AnyAsync(s => s.Name == subject.Name))
            ModelState.AddModelError(nameof(subject.Name), "That subject already exists.");

        if (!ModelState.IsValid)
        {
            ViewBag.Subjects = await _context.Subjects
                .Include(s => s.BatchSubjects)
                .OrderBy(s => s.Name)
                .ToListAsync();

            return View(nameof(Index), subject);
        }

        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Subject \"{subject.Name}\" added.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Admin/Subjects/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == id);

        if (subject == null)
            return NotFound();

        return View(subject);
    }

    // POST: /Admin/Subjects/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Subject subject)
    {
        if (id != subject.Id)
            return NotFound();

        if (await _context.Subjects.AnyAsync(s => s.Name == subject.Name && s.Id != id))
            ModelState.AddModelError(nameof(subject.Name), "That subject already exists.");

        if (!ModelState.IsValid)
            return View(subject);

        var existing = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == id);

        if (existing == null)
            return NotFound();

        existing.Name = subject.Name;
        existing.Code = subject.Code;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Subject updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /Admin/Subjects/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == id);

        if (subject == null)
            return NotFound();

        // Cascade would take recorded marks down with it.
        if (await _context.BatchSubjects.AnyAsync(bs => bs.SubjectId == id)
            || await _context.Marks.AnyAsync(m => m.SubjectId == id))
            TempData["Error"] = $"\"{subject.Name}\" is in use by a batch or has marks recorded. Remove those first.";
        else
        {
            _context.Subjects.Remove(subject);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Subject deleted successfully.";
        }

        return RedirectToAction(nameof(Index));
    }
}
