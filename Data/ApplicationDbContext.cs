using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TSC.Models;

namespace TSC.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<SchoolClass> SchoolClasses => Set<SchoolClass>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<ClassSubject> ClassSubjects => Set<ClassSubject>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamSubject> ExamSubjects => Set<ExamSubject>();
    public DbSet<Mark> Marks => Set<Mark>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Notice> Notices => Set<Notice>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Student>().HasIndex(s => s.StudentId).IsUnique();
        builder.Entity<Teacher>().HasIndex(t => t.TeacherId).IsUnique();
        builder.Entity<SchoolClass>().HasIndex(b => b.Name).IsUnique();
        builder.Entity<ClassSubject>().HasIndex(bs => new { bs.SchoolClassId, bs.SubjectId }).IsUnique();

        // Two classes on the same level would generate colliding Student IDs.
        builder.Entity<SchoolClass>().HasIndex(c => c.Level).IsUnique();

        // One row per student per day: taking attendance twice updates, never duplicates.
        builder.Entity<Attendance>().HasIndex(a => new { a.StudentId, a.Date }).IsUnique();

        builder.Entity<ExamSubject>().HasIndex(es => new { es.ExamId, es.SubjectId }).IsUnique();

        // One mark per student per subject per exam.
        builder.Entity<Mark>().HasIndex(m => new { m.ExamId, m.StudentId, m.SubjectId }).IsUnique();

        // A portal login belongs to exactly one person. Postgres allows many NULLs here,
        // so unlinked records are unaffected.
        builder.Entity<Student>().HasIndex(s => s.UserId).IsUnique();
        builder.Entity<Teacher>().HasIndex(t => t.UserId).IsUnique();

        // Archived students disappear from every query in the app. Use IgnoreQueryFilters()
        // where an archived student still matters -- the archive list, and the duplicate
        // StudentId check, which must still see IDs held by archived records.
        builder.Entity<Student>().Property(s => s.IsActive).HasDefaultValue(true);
        builder.Entity<Student>().HasQueryFilter(s => s.IsActive);

        // Matching filters on the dependents. Without these EF warns, and worse, a mark or
        // payment belonging to an archived student would still be returned with a null
        // Student navigation. The rows stay in the database -- IgnoreQueryFilters() reads
        // them back if an archive report ever needs the history.
        builder.Entity<Mark>().HasQueryFilter(m => m.Student!.IsActive);
        builder.Entity<Attendance>().HasQueryFilter(a => a.Student!.IsActive);
        builder.Entity<Payment>().HasQueryFilter(p => p.Student!.IsActive);
    }
}
