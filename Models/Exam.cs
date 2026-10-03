using System.ComponentModel.DataAnnotations;

namespace TSC.Models;

public class Exam
{
    public int Id { get; set; }

    [Required, StringLength(80), Display(Name = "Exam Name")]
    public string Name { get; set; } = string.Empty;

    [DataType(DataType.Date), Display(Name = "Exam Date")]
    public DateOnly ExamDate { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select a class.")]
    [Display(Name = "Class")]
    public int SchoolClassId { get; set; }
    public SchoolClass? SchoolClass { get; set; }

    public ICollection<ExamSubject> ExamSubjects { get; set; } = new List<ExamSubject>();

    public ICollection<Mark> Marks { get; set; } = new List<Mark>();
}
