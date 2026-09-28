using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TSC.Data;
using TSC.Models;

namespace TSC.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class NoticesController : Controller
{
    private readonly ApplicationDbContext _context;

    public NoticesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Admin/Notices
    public async Task<IActionResult> Index() =>
        View(await _context.Notices.OrderByDescending(n => n.PublishedAt).ToListAsync());

    // GET: /Admin/Notices/Create
    [HttpGet]
    public IActionResult Create() => View(new Notice());

    // POST: /Admin/Notices/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Notice notice)
    {
        if (!ModelState.IsValid)
            return View(notice);

        notice.PublishedAt = DateTime.UtcNow;

        _context.Notices.Add(notice);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Notice published.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Admin/Notices/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var notice = await _context.Notices.FirstOrDefaultAsync(n => n.Id == id);

        if (notice == null)
            return NotFound();

        return View(notice);
    }

    // POST: /Admin/Notices/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Notice notice)
    {
        if (id != notice.Id)
            return NotFound();

        if (!ModelState.IsValid)
            return View(notice);

        var existing = await _context.Notices.FirstOrDefaultAsync(n => n.Id == id);

        if (existing == null)
            return NotFound();

        // PublishedAt deliberately untouched: editing a typo should not reorder the board.
        existing.Title = notice.Title;
        existing.Content = notice.Content;
        existing.IsActive = notice.IsActive;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Notice updated.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /Admin/Notices/Toggle/5  -- pull a notice down without losing it
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var notice = await _context.Notices.FirstOrDefaultAsync(n => n.Id == id);

        if (notice == null)
            return NotFound();

        notice.IsActive = !notice.IsActive;
        await _context.SaveChangesAsync();

        TempData["Success"] = notice.IsActive ? "Notice is now visible." : "Notice hidden.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /Admin/Notices/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var notice = await _context.Notices.FirstOrDefaultAsync(n => n.Id == id);

        if (notice == null)
            return NotFound();

        _context.Notices.Remove(notice);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Notice deleted.";

        return RedirectToAction(nameof(Index));
    }
}
