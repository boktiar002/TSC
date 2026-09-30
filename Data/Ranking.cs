using Microsoft.EntityFrameworkCore;
using TSC.Models;

namespace TSC.Data;

// The merit sheet the centre prints by hand, worked out once for both the office and the
// teacher looking at their own batch.
public static class Ranking
{
    // Every marked paper of the selected exams, in the order the papers were added, which is
    // the order they appear on the printed sheets.
    public static List<(Exam Exam, ExamSubject Paper)> PapersOf(List<Exam> exams) =>
        exams
            .SelectMany(e => e.ExamSubjects.OrderBy(es => es.Id).Select(es => (Exam: e, Paper: es)))
            .ToList();

    // The whole batch scored and placed. An absent paper scores zero, so every selected paper
    // counts towards the denominator; equal totals share a place and the next total takes the
    // next place (1, 1, 2, 3), the convention the centre's own sheets use.
    public static async Task<List<ProgressRow>> RankAsync(
        ApplicationDbContext db, int batchId, List<Exam> chosen, List<(Exam Exam, ExamSubject Paper)> columns)
    {
        var chosenIds = chosen.Select(e => e.Id).ToList();

        var students = await db.Students
            .Where(s => s.BatchId == batchId)
            .OrderBy(s => s.StudentId)
            .ToListAsync();

        var marks = await db.Marks
            .Where(m => chosenIds.Contains(m.ExamId))
            .Select(m => new { m.StudentId, m.ExamId, m.SubjectId, m.Score })
            .ToListAsync();

        // Unique on (exam, student, subject), so one score per cell.
        var byCell = marks.ToDictionary(m => (m.StudentId, m.ExamId, m.SubjectId), m => m.Score);
        var fullMarks = columns.Sum(c => c.Paper.FullMarks);

        var rows = students
            .Select(s =>
            {
                var scores = columns
                    .Select(c => byCell.TryGetValue((s.Id, c.Exam.Id, c.Paper.SubjectId), out var score)
                        ? score
                        : (decimal?)null)
                    .ToArray();

                var total = scores.Sum(score => score ?? 0);

                return new ProgressRow
                {
                    Student = s,
                    Scores = scores,
                    Total = total,
                    PapersSat = scores.Count(score => score != null),
                    AveragePercent = fullMarks == 0 ? 0 : Math.Round(total / fullMarks * 100, 1),
                };
            })
            .OrderByDescending(r => r.Total)
            .ThenBy(r => r.Student.StudentId)
            .ToList();

        var place = 0;
        decimal? previous = null;

        foreach (var row in rows)
        {
            if (row.Total != previous)
            {
                place++;
                previous = row.Total;
            }

            row.Rank = place;
        }

        return rows;
    }
}
