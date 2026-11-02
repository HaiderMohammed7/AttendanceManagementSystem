namespace AttendanceManagementSystem.Models;

public sealed class Student
{
    public int Id { get; set; }

    public string UniversityId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int CourseId { get; set; }

    public Course Course { get; set; } = null!;

    public ICollection<AttendanceRecord> AttendanceRecords { get; } = new List<AttendanceRecord>();
}
