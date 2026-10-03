using Microsoft.EntityFrameworkCore;
using TSC.Models;

namespace TSC.Data;

// Shared by the admin mark sheet and the teacher's own-paper mark sheet, so the two cannot
// drift apart on validation or on the blank-means-unmarked rule.
public static class MarkEntry
{
    public record Result(int Saved, List<string> Rejected);

    public static async Task<Result> SaveAsync(
        ApplicationDbContext context, Exam exam, ExamSubject paper, int[] studentIds, string?[] scores)
    {
        // Re-read the roster: a tampered form must not reach a student in another class.
        var roster = await context.Students
            .Where(s => s.SchoolClassId == exam.SchoolClassId)
            .Select(s => s.Id)
            .ToListAsync();

        var existing = await context.Marks
            .Where(m => m.ExamId == exam.Id && m.SubjectId == paper.SubjectId)
            .ToDictionaryAsync(m => m.StudentId);

        var saved = 0;
        var rejected = new List<string>();

        for (var i = 0; i < studentIds.Length && i < scores.Length; i++)
        {
            if (!roster.Contains(studentIds[i]))
                continue;

            var raw = scores[i]?.Trim();

            // Blank means "not marked yet" and clears any existing score, so a mistyped
            // entry is undone by emptying the box rather than guessing a zero.
            if (string.IsNullOrEmpty(raw))
            {
                if (existing.TryGetValue(studentIds[i], out var blank))
                    context.Marks.Remove(blank);

                continue;
            }

            if (!decimal.TryParse(raw, out var value) || value < 0 || value > paper.FullMarks)
            {
                rejected.Add(raw);
                continue;
            }

            if (existing.TryGetValue(studentIds[i], out var row))
                row.Score = value;
            else
                context.Marks.Add(new Mark
                {
                    ExamId = exam.Id,
                    SubjectId = paper.SubjectId,
                    StudentId = studentIds[i],
                    Score = value
                });

            saved++;
        }

        await context.SaveChangesAsync();

        return new Result(saved, rejected);
    }
}
