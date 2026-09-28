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
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<BatchSubject> BatchSubjects => Set<BatchSubject>();
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
        builder.Entity<Batch>().HasIndex(b => b.Name).IsUnique();
        builder.Entity<BatchSubject>().HasIndex(bs => new { bs.BatchId, bs.SubjectId }).IsUnique();

        // One row per student per day: taking attendance twice updates, never duplicates.
        builder.Entity<Attendance>().HasIndex(a => new { a.StudentId, a.Date }).IsUnique();

        builder.Entity<ExamSubject>().HasIndex(es => new { es.ExamId, es.SubjectId }).IsUnique();

        // One mark per student per subject per exam.
        builder.Entity<Mark>().HasIndex(m => new { m.ExamId, m.StudentId, m.SubjectId }).IsUnique();

        // A portal login belongs to exactly one person. Postgres allows many NULLs here,
        // so unlinked records are unaffected.
        builder.Entity<Student>().HasIndex(s => s.UserId).IsUnique();
        builder.Entity<Teacher>().HasIndex(t => t.UserId).IsUnique();
    }
}
