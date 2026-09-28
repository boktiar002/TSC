using System.ComponentModel.DataAnnotations;

namespace TSC.Models;

// Which subjects an exam covers, and what each is marked out of.
// FullMarks belongs here, not on Mark: it is the same for every student sitting the paper.
public class ExamSubject
{
    public int Id { get; set; }

    public int ExamId { get; set; }
    public Exam? Exam { get; set; }

    [Display(Name = "Subject")]
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    [Range(1, 1000), Display(Name = "Full Marks")]
    public decimal FullMarks { get; set; } = 100;
}
