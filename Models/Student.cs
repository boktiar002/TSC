using System.ComponentModel.DataAnnotations;

namespace TSC.Models;

public class Student
{
    public int Id { get; set; }

    [Required, StringLength(20), Display(Name = "Student ID")]
    public string StudentId { get; set; } = string.Empty;

    [Required, StringLength(120), Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? Phone { get; set; }

    [EmailAddress, StringLength(120)]
    public string? Email { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(120), Display(Name = "Guardian Name")]
    public string? GuardianName { get; set; }

    [Phone, StringLength(20), Display(Name = "Guardian Phone")]
    public string? GuardianPhone { get; set; }

    // date, not timestamp: a birthday has no time and no time zone.
    [DataType(DataType.Date), Display(Name = "Date of Birth")]
    public DateOnly? DateOfBirth { get; set; }

    // The two sittings the centre runs. A batch is not a class: both branches sit the same
    // paper and are ranked together, so it only says which sitting a child turns up for.
    // This is the list the form offers; AllowedValues below is what enforces it, because a
    // posted form is not bound by what the dropdown happened to contain.
    public static readonly string[] Batches = { "Morning", "Evening" };

    [StringLength(20)]
    [AllowedValues(null, "Morning", "Evening", ErrorMessage = "Batch must be Morning or Evening.")]
    [Display(Name = "Batch")]
    public string? Batch { get; set; }

    // Students who leave are archived, not deleted: the centre still needs their fee and
    // mark history. A global query filter keeps them out of every roster automatically.
    public bool IsActive { get; set; } = true;

    [Range(1, int.MaxValue, ErrorMessage = "Please select a class.")]
    [Display(Name = "Class")]
    public int SchoolClassId { get; set; }
    public SchoolClass? SchoolClass { get; set; }

    public string? UserId { get; set; }
}
