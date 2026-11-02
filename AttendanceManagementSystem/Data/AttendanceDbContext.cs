using Microsoft.EntityFrameworkCore;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Data;

public sealed class AttendanceDbContext(DbContextOptions<AttendanceDbContext> options) : DbContext(options)
{
    public DbSet<Course> Courses => Set<Course>();

    public DbSet<Student> Students => Set<Student>();

    public DbSet<LectureSession> LectureSessions => Set<LectureSession>();

    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(course => course.Id);

            entity.Property(course => course.CourseName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(course => course.InstructorName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(course => course.AcademicYear)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(course => course.Semester)
                .HasMaxLength(50)
                .IsRequired();

            entity.HasMany(course => course.Students)
                .WithOne(student => student.Course)
                .HasForeignKey(student => student.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(course => course.LectureSessions)
                .WithOne(session => session.Course)
                .HasForeignKey(session => session.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(student => student.Id);

            entity.Property(student => student.UniversityId)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(student => student.FullName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(student => student.IsActive)
                .HasDefaultValue(true)
                .IsRequired();

            entity.HasIndex(student => student.UniversityId)
                .IsUnique();

            entity.HasMany(student => student.AttendanceRecords)
                .WithOne(record => record.Student)
                .HasForeignKey(record => record.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LectureSession>(entity =>
        {
            entity.HasKey(session => session.Id);

            entity.Property(session => session.Title)
                .HasMaxLength(200);

            entity.Property(session => session.Status)
                .HasDefaultValue(LectureSessionStatus.Draft)
                .IsRequired();

            entity.HasMany(session => session.AttendanceRecords)
                .WithOne(record => record.LectureSession)
                .HasForeignKey(record => record.LectureSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasKey(record => record.Id);

            entity.HasIndex(record => new { record.LectureSessionId, record.StudentId })
                .IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}
