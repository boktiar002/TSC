using System.ComponentModel.DataAnnotations;

namespace TSC.Models;

public class Teacher
{
    public int Id { get; set; }

    [Required, StringLength(20), Display(Name = "Teacher ID")]
    public string TeacherId { get; set; } = string.Empty;

    [Required, StringLength(120), Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? Phone { get; set; }

    [EmailAddress, StringLength(120)]
    public string? Email { get; set; }

    [StringLength(120)]
    public string? Specialization { get; set; }

    public string? UserId { get; set; }

    public ICollection<BatchSubject> BatchSubjects { get; set; } = new List<BatchSubject>();
}
