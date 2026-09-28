using TSC.Models;

namespace TSC.Models;

public class BatchSubject
{
    public int Id { get; set; }

    public int BatchId { get; set; }
    public Batch? Batch { get; set; }

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
}