using System.ComponentModel.DataAnnotations;

namespace TSC.Models;

public class Mark
{
    public int Id { get; set; }

    public int StudentId { get; set; }
    public Student? Student { get; set; }

    public int ExamId { get; set; }
    public Exam? Exam { get; set; }

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    [Range(0, 1000)]
    public decimal Score { get; set; }
}
