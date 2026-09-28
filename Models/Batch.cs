using System.ComponentModel.DataAnnotations;

namespace TSC.Models;

public class Batch
{
    public int Id { get; set; }

    [Required, StringLength(60), Display(Name = "Batch Name")]
    public string Name { get; set; } = string.Empty;

    [Range(0, 1000000), Display(Name = "Monthly Fee")]
    public decimal MonthlyFee { get; set; }

    [StringLength(250)]
    public string? Description { get; set; }

    public ICollection<Student> Students { get; set; } = new List<Student>();

    public ICollection<BatchSubject> BatchSubjects { get; set; } = new List<BatchSubject>();

    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
}
