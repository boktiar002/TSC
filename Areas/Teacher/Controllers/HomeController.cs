using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TSC.Data;
using TSC.Models;

namespace TSC.Areas.TeacherPortal.Controllers;

[Area("Teacher")]
[Authorize(Roles = "Teacher")]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var me = await MeAsync();

        if (me == null)
            return View("NotLinked");

        var batchIds = me.BatchSubjects.Select(bs => bs.BatchId).Distinct().ToList();

        ViewBag.StudentCount = await _context.Students.CountAsync(s => batchIds.Contains(s.BatchId));
        ViewBag.BatchCount = batchIds.Count;
        ViewBag.Notices = await ActiveNoticesAsync();

        return View(me);
    }

    // GET: /Teacher/Home/Batch/3  -- roster of one batch this teacher actually teaches
    public async Task<IActionResult> Batch(int id)
    {
        var me = await MeAsync();

        if (me == null)
            return View("NotLinked");

        if (me.BatchSubjects.All(bs => bs.BatchId != id))
            return Forbid();

        var batch = await _context.Batches.FirstOrDefaultAsync(b => b.Id == id);

        if (batch == null)
            return NotFound();

        ViewBag.Batch = batch;
        ViewBag.MySubjects = me.BatchSubjects
            .Where(bs => bs.BatchId == id)
            .Select(bs => bs.Subject!)
            .OrderBy(s => s.Name)
            .ToList();

        return View(await _context.Students
            .Where(s => s.BatchId == id)
            .OrderBy(s => s.FullName)
            .ToListAsync());
    }

    // GET: /Teacher/Home/Marks/5?subjectId=2
    public async Task<IActionResult> Marks(int id, int? subjectId)
    {
        var me = await MeAsync();

        if (me == null)
            return View("NotLinked");

        var exam = await LoadExamAsync(id);

        if (exam == null)
            return NotFound();

        // A teacher may only touch papers for the (batch, subject) pairs assigned to them.
        var mine = MyPapers(me, exam);

        if (mine.Count == 0)
            return Forbid();

        var paper = subjectId == null
            ? mine.OrderBy(es => es.Subject!.Name).First()
            : mine.FirstOrDefault(es => es.SubjectId == subjectId);

        if (paper == null)
            return Forbid();

        ViewBag.Exam = exam;
        ViewBag.Paper = paper;
        ViewBag.MyPapers = mine;
        ViewBag.Entered = await _context.Marks
            .Where(m => m.ExamId == id && m.SubjectId == paper.SubjectId)
            .ToDictionaryAsync(m => m.StudentId, m => m.Score);

        return View(await _context.Students
            .Where(s => s.BatchId == exam.BatchId)
            .OrderBy(s => s.FullName)
            .ToListAsync());
    }

    // POST: /Teacher/Home/Marks/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Marks(int id, int subjectId, int[] studentId, string?[] score)
    {
        var me = await MeAsync();

        if (me == null)
            return View("NotLinked");

        var exam = await LoadExamAsync(id);

        if (exam == null)
            return NotFound();

        var paper = MyPapers(me, exam).FirstOrDefault(es => es.SubjectId == subjectId);

        if (paper == null)
            return Forbid();

        var result = await MarkEntry.SaveAsync(_context, exam, paper, studentId, score);

        if (result.Rejected.Count > 0)
            TempData["Error"] = $"Ignored {result.Rejected.Count} entry(s) — a score must be a number from 0 to {paper.FullMarks:0.##}: {string.Join(", ", result.Rejected)}";

        TempData["Success"] = $"Saved {result.Saved} mark(s) for {paper.Subject?.Name}.";

        return RedirectToAction(nameof(Marks), new { id, subjectId });
    }

    public async Task<IActionResult> Exams()
    {
        var me = await MeAsync();

        if (me == null)
            return View("NotLinked");

        var batchIds = me.BatchSubjects.Select(bs => bs.BatchId).Distinct().ToList();

        var exams = await _context.Exams
            .Where(e => batchIds.Contains(e.BatchId))
            .Include(e => e.Batch)
            .Include(e => e.ExamSubjects).ThenInclude(es => es.Subject)
            .OrderByDescending(e => e.ExamDate)
            .ToListAsync();

        // Only exams that include at least one paper this teacher is responsible for.
        ViewBag.Teacher = me;

        return View(exams.Where(e => MyPapers(me, e).Count > 0).ToList());
    }

    public async Task<IActionResult> Notices() => View(await ActiveNoticesAsync());

    // The (batch, subject) pairs this teacher is assigned, intersected with the exam's papers.
    private static List<ExamSubject> MyPapers(Teacher me, Exam exam)
    {
        var mySubjectIds = me.BatchSubjects
            .Where(bs => bs.BatchId == exam.BatchId)
            .Select(bs => bs.SubjectId)
            .ToHashSet();

        return exam.ExamSubjects.Where(es => mySubjectIds.Contains(es.SubjectId)).ToList();
    }

    private Task<Exam?> LoadExamAsync(int id) =>
        _context.Exams
            .Include(e => e.Batch)
            .Include(e => e.ExamSubjects).ThenInclude(es => es.Subject)
            .FirstOrDefaultAsync(e => e.Id == id);

    private Task<Teacher?> MeAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return _context.Teachers
            .Include(t => t.BatchSubjects).ThenInclude(bs => bs.Batch)
            .Include(t => t.BatchSubjects).ThenInclude(bs => bs.Subject)
            .FirstOrDefaultAsync(t => t.UserId == userId);
    }

    private Task<List<Notice>> ActiveNoticesAsync() =>
        _context.Notices
            .Where(n => n.IsActive)
            .OrderByDescending(n => n.PublishedAt)
            .ToListAsync();
}
