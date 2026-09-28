using System.ComponentModel.DataAnnotations;

namespace TSC.Models;

public class Exam
{
    public int Id { get; set; }

    [Required, StringLength(80), Display(Name = "Exam Name")]
    public string Name { get; set; } = string.Empty;

    [DataType(DataType.Date), Display(Name = "Exam Date")]
    public DateOnly ExamDate { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select a batch.")]
    [Display(Name = "Batch")]
    public int BatchId { get; set; }
    public Batch? Batch { get; set; }

    public ICollection<ExamSubject> ExamSubjects { get; set; } = new List<ExamSubject>();

    public ICollection<Mark> Marks { get; set; } = new List<Mark>();
}
