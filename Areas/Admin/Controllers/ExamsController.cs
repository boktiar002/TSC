using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TSC.Data;
using TSC.Models;

namespace TSC.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ExamsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ExamsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Admin/Exams
    public async Task<IActionResult> Index()
    {
        var exams = await _context.Exams
            .Include(e => e.Batch)
            .Include(e => e.ExamSubjects)
            .Include(e => e.Marks)
            .OrderByDescending(e => e.ExamDate)
            .ToListAsync();

        return View(exams);
    }

    // GET: /Admin/Exams/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadBatches();

        return View(new Exam { ExamDate = DateOnly.FromDateTime(DateTime.Today) });
    }

    // POST: /Admin/Exams/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Exam exam)
    {
        if (await _context.Exams.AnyAsync(e => e.Name == exam.Name && e.BatchId == exam.BatchId))
            ModelState.AddModelError(nameof(exam.Name), "That batch already has an exam with this name.");

        if (!ModelState.IsValid)
        {
            await LoadBatches();
            return View(exam);
        }

        _context.Exams.Add(exam);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Exam created. Now add the subjects it covers.";

        return RedirectToAction(nameof(Details), new { id = exam.Id });
    }

    // GET: /Admin/Exams/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        var exam = await LoadExam(id);

        if (exam == null)
            return NotFound();

        // Only the subjects actually taught to this batch, minus the ones already on the paper.
        var onPaper = exam.ExamSubjects.Select(es => es.SubjectId).ToList();

        ViewBag.AvailableSubjects = await _context.BatchSubjects
            .Where(bs => bs.BatchId == exam.BatchId && !onPaper.Contains(bs.SubjectId))
            .Select(bs => bs.Subject!)
            .OrderBy(s => s.Name)
            .ToListAsync();

        ViewBag.StudentCount = await _context.Students.CountAsync(s => s.BatchId == exam.BatchId);

        return View(exam);
    }

    // POST: /Admin/Exams/AddSubject/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSubject(int id, int subjectId, decimal fullMarks)
    {
        var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == id);

        if (exam == null)
            return NotFound();

        if (fullMarks <= 0)
            TempData["Error"] = "Full marks must be greater than zero.";
        else if (!await _context.BatchSubjects.AnyAsync(bs => bs.BatchId == exam.BatchId && bs.SubjectId == subjectId))
            TempData["Error"] = "That subject is not taught to this batch.";
        else if (await _context.ExamSubjects.AnyAsync(es => es.ExamId == id && es.SubjectId == subjectId))
            TempData["Error"] = "That subject is already on this exam.";
        else
        {
            _context.ExamSubjects.Add(new ExamSubject
            {
                ExamId = id,
                SubjectId = subjectId,
                FullMarks = fullMarks
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Subject added to the exam.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Admin/Exams/RemoveSubject/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSubject(int id, int examSubjectId)
    {
        var examSubject = await _context.ExamSubjects
            .FirstOrDefaultAsync(es => es.Id == examSubjectId && es.ExamId == id);

        if (examSubject == null)
            return NotFound();

        // Removing the paper would orphan every score already entered for it.
        if (await _context.Marks.AnyAsync(m => m.ExamId == id && m.SubjectId == examSubject.SubjectId))
            TempData["Error"] = "Marks have already been entered for that subject. Clear them first.";
        else
        {
            _context.ExamSubjects.Remove(examSubject);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Subject removed from the exam.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Admin/Exams/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == id);

        if (exam == null)
            return NotFound();

        if (await _context.Marks.AnyAsync(m => m.ExamId == id))
            TempData["Error"] = $"\"{exam.Name}\" has marks recorded. Clear them before deleting the exam.";
        else
        {
            _context.Exams.Remove(exam);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Exam deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    // GET: /Admin/Exams/Marks/5?subjectId=2  -- score sheet for one paper
    public async Task<IActionResult> Marks(int id, int? subjectId)
    {
        var exam = await LoadExam(id);

        if (exam == null)
            return NotFound();

        if (!exam.ExamSubjects.Any())
        {
            TempData["Error"] = "Add at least one subject to this exam first.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var paper = subjectId == null
            ? exam.ExamSubjects.OrderBy(es => es.Subject!.Name).First()
            : exam.ExamSubjects.FirstOrDefault(es => es.SubjectId == subjectId);

        if (paper == null)
            return NotFound();

        var roster = await _context.Students
            .Where(s => s.BatchId == exam.BatchId)
            .OrderBy(s => s.FullName)
            .ToListAsync();

        ViewBag.Exam = exam;
        ViewBag.Paper = paper;
        ViewBag.Entered = await _context.Marks
            .Where(m => m.ExamId == id && m.SubjectId == paper.SubjectId)
            .ToDictionaryAsync(m => m.StudentId, m => m.Score);

        return View(roster);
    }

    // POST: /Admin/Exams/Marks/5
    // A blank box means "not marked yet" and clears any existing score, so a mistyped entry
    // can be undone by emptying the field rather than guessing a zero.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Marks(int id, int subjectId, int[] studentId, string?[] score)
    {
        var exam = await _context.Exams
            .Include(e => e.ExamSubjects).ThenInclude(es => es.Subject)
            .FirstOrDefaultAsync(e => e.Id == id);

        var paper = exam?.ExamSubjects.FirstOrDefault(es => es.SubjectId == subjectId);

        if (exam == null || paper == null)
            return NotFound();

        var result = await MarkEntry.SaveAsync(_context, exam, paper, studentId, score);

        if (result.Rejected.Count > 0)
            TempData["Error"] = $"Ignored {result.Rejected.Count} entry(s) — a score must be a number from 0 to {paper.FullMarks:0.##}: {string.Join(", ", result.Rejected)}";

        TempData["Success"] = $"Saved {result.Saved} mark(s) for {paper.Subject?.Name}.";

        return RedirectToAction(nameof(Marks), new { id, subjectId });
    }

    // GET: /Admin/Exams/Results/5  -- the full result sheet
    public async Task<IActionResult> Results(int id)
    {
        var exam = await LoadExam(id);

        if (exam == null)
            return NotFound();

        ViewBag.Exam = exam;

        ViewBag.Students = await _context.Students
            .Where(s => s.BatchId == exam.BatchId)
            .OrderBy(s => s.FullName)
            .ToListAsync();

        // (studentId, subjectId) -> score, so the grid renders without a query per cell.
        ViewBag.Scores = await _context.Marks
            .Where(m => m.ExamId == id)
            .ToDictionaryAsync(m => (m.StudentId, m.SubjectId), m => m.Score);

        return View();
    }

    private async Task<Exam?> LoadExam(int? id) =>
        id == null
            ? null
            : await _context.Exams
                .Include(e => e.Batch)
                .Include(e => e.ExamSubjects).ThenInclude(es => es.Subject)
                .FirstOrDefaultAsync(e => e.Id == id);

    private async Task LoadBatches() =>
        ViewBag.Batches = await _context.Batches.OrderBy(b => b.Name).ToListAsync();
}
