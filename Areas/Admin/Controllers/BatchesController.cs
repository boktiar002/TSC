using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TSC.Data;
using TSC.Models;

namespace TSC.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class BatchesController : Controller
{
    private readonly ApplicationDbContext _context;

    public BatchesController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var batches = await _context.Batches
            .Include(b => b.Students)
            .Include(b => b.BatchSubjects)
            .OrderBy(b => b.Name)
            .ToListAsync();

        return View(batches);
    }

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Batch batch)
    {
        if (await _context.Batches.AnyAsync(b => b.Name == batch.Name))
            ModelState.AddModelError(nameof(batch.Name), "A batch with that name already exists.");

        if (!ModelState.IsValid)
            return View(batch);

        _context.Batches.Add(batch);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Batch created successfully.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var batch = await _context.Batches.FirstOrDefaultAsync(b => b.Id == id);

        if (batch == null)
            return NotFound();

        return View(batch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Batch batch)
    {
        if (id != batch.Id)
            return NotFound();

        if (await _context.Batches.AnyAsync(b => b.Name == batch.Name && b.Id != id))
            ModelState.AddModelError(nameof(batch.Name), "A batch with that name already exists.");

        if (!ModelState.IsValid)
            return View(batch);

        var existing = await _context.Batches.FirstOrDefaultAsync(b => b.Id == id);

        if (existing == null)
            return NotFound();

        existing.Name = batch.Name;
        existing.Description = batch.Description;
        existing.MonthlyFee = batch.MonthlyFee;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Batch updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var batch = await _context.Batches
            .Include(b => b.Students)
            .Include(b => b.BatchSubjects)
                .ThenInclude(bs => bs.Subject)
            .Include(b => b.BatchSubjects)
                .ThenInclude(bs => bs.Teacher)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (batch == null)
            return NotFound();

        await LoadAssignmentOptions(batch);

        return View(batch);
    }

    // POST: /Admin/Batches/AssignSubject/5  -- assign a subject + its teacher to this batch
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignSubject(int id, int subjectId, int teacherId)
    {
        if (!await _context.Batches.AnyAsync(b => b.Id == id))
            return NotFound();

        if (await _context.BatchSubjects.AnyAsync(bs => bs.BatchId == id && bs.SubjectId == subjectId))
            TempData["Error"] = "That subject is already assigned to this batch.";
        else
        {
            _context.BatchSubjects.Add(new BatchSubject
            {
                BatchId = id,
                SubjectId = subjectId,
                TeacherId = teacherId
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Subject assigned to batch.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Admin/Batches/RemoveSubject/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSubject(int id, int batchSubjectId)
    {
        var assignment = await _context.BatchSubjects
            .FirstOrDefaultAsync(bs => bs.Id == batchSubjectId && bs.BatchId == id);

        if (assignment == null)
            return NotFound();

        _context.BatchSubjects.Remove(assignment);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Assignment removed.";

        return RedirectToAction(nameof(Details), new { id });
    }

    // Only offer subjects not already on this batch.
    private async Task LoadAssignmentOptions(Batch batch)
    {
        var taken = batch.BatchSubjects.Select(bs => bs.SubjectId).ToList();

        ViewBag.AvailableSubjects = await _context.Subjects
            .Where(s => !taken.Contains(s.Id))
            .OrderBy(s => s.Name)
            .ToListAsync();

        ViewBag.Teachers = await _context.Teachers
            .OrderBy(t => t.FullName)
            .ToListAsync();
    }
}
