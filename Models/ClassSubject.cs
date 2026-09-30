namespace TSC.Models;

public class ClassSubject
{
    public int Id { get; set; }

    public int SchoolClassId { get; set; }
    public SchoolClass? SchoolClass { get; set; }

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
}
