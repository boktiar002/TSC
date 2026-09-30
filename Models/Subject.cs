using System.ComponentModel.DataAnnotations;

namespace TSC.Models;

public class Subject
{
    public int Id { get; set; }

    [Required, StringLength(80), Display(Name = "Subject Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Code { get; set; }

    public ICollection<ClassSubject> ClassSubjects { get; set; } = new List<ClassSubject>();

    public ICollection<ExamSubject> ExamSubjects { get; set; } = new List<ExamSubject>();

    public ICollection<Mark> Marks { get; set; } = new List<Mark>();
}
