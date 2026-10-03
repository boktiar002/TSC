using System.ComponentModel.DataAnnotations;

namespace TSC.Models;

// One of the four classes the centre runs: 2, 3, 4 and 5. Morning and evening are not
// separate classes -- both branches sit the same paper and are ranked together, so exams,
// subjects, fees, ranking and attendance are all per class. Which branch a child attends
// is Student.Batch.
public class SchoolClass
{
    public int Id { get; set; }

    [Required, StringLength(60), Display(Name = "Class Name")]
    public string Name { get; set; } = string.Empty;

    // 2, 3, 4 or 5. Student IDs and sorting read this rather than parsing "Class 3".
    [Range(1, 12), Display(Name = "Class Level")]
    public int Level { get; set; }

    [Range(0, 1000000), Display(Name = "Monthly Fee")]
    public decimal MonthlyFee { get; set; }

    [StringLength(250)]
    public string? Description { get; set; }

    public ICollection<Student> Students { get; set; } = new List<Student>();

    public ICollection<ClassSubject> ClassSubjects { get; set; } = new List<ClassSubject>();

    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
}
