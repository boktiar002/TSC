namespace TSC.Models;

// One batch's sheet for one day, as the roll call screen needs it.
public class RollCall
{
    public int BatchId { get; init; }
    public DateOnly Date { get; init; }
    public IReadOnlyList<Student> Roster { get; init; } = [];

    // Student id -> present. A student missing from here has not been marked at all yet.
    public IReadOnlyDictionary<int, bool> Recorded { get; init; } = new Dictionary<int, bool>();

    public int PresentCount => Recorded.Count(r => r.Value);
}
