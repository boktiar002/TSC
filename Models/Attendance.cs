using System.ComponentModel.DataAnnotations;

namespace TSC.Models;

public class Attendance
{
    public int Id { get; set; }

    public int StudentId { get; set; }
    public Student? Student { get; set; }

    [DataType(DataType.Date)]
    public DateOnly Date { get; set; }

    public bool IsPresent { get; set; }
}
