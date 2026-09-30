using Microsoft.EntityFrameworkCore;
using TSC.Models;

namespace TSC.Data;

// The roll call is taken by the office and by the teacher standing in front of the class, so
// the rules live here rather than in either controller.
public static class RollCallBook
{
    public static async Task<RollCall> LoadAsync(ApplicationDbContext db, int batchId, DateOnly date)
    {
        // Roll order, because that is the order the names get called out.
        var roster = await db.Students
            .Where(s => s.BatchId == batchId)
            .OrderBy(s => s.StudentId)
            .ToListAsync();

        var ids = roster.Select(s => s.Id).ToList();

        var recorded = await db.Attendances
            .Where(a => a.Date == date && ids.Contains(a.StudentId))
            .ToDictionaryAsync(a => a.StudentId, a => a.IsPresent);

        return new RollCall
        {
            BatchId = batchId,
            Date = date,
            Roster = roster,
            Recorded = recorded,
        };
    }

    // One tap: present becomes absent and back again. Returns null when the student is not in
    // that batch, which is what a tampered form looks like.
    public static async Task<(bool Present, int PresentCount, int Total)?> ToggleAsync(
        ApplicationDbContext db, int batchId, int studentId, DateOnly date)
    {
        var roster = await db.Students
            .Where(s => s.BatchId == batchId)
            .Select(s => s.Id)
            .ToListAsync();

        if (!roster.Contains(studentId))
            return null;

        var existing = await db.Attendances
            .Where(a => a.Date == date && roster.Contains(a.StudentId))
            .ToListAsync();

        // The first tap of the day opens the sheet for the whole batch, every one of them
        // absent. Without this, a student nobody called would have no row at all and would
        // quietly drop out of the attendance rate instead of counting against it.
        if (existing.Count == 0)
        {
            existing = roster
                .Select(id => new Attendance { StudentId = id, Date = date, IsPresent = false })
                .ToList();

            db.Attendances.AddRange(existing);
        }

        var row = existing.FirstOrDefault(a => a.StudentId == studentId);

        if (row == null)
        {
            // A student added to the batch after the sheet was opened.
            row = new Attendance { StudentId = studentId, Date = date, IsPresent = false };
            db.Attendances.Add(row);
            existing.Add(row);
        }

        row.IsPresent = !row.IsPresent;

        await db.SaveChangesAsync();

        return (row.IsPresent, existing.Count(a => a.IsPresent), roster.Count);
    }
}
