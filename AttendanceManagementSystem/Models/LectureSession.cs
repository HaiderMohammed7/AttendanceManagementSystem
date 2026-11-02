namespace AttendanceManagementSystem.Models;

public sealed class LectureSession
{
    public int Id { get; set; }

    public int CourseId { get; set; }

    public DateTime LectureDate { get; set; }

    public string? Title { get; set; }

    public LectureSessionStatus Status { get; set; } = LectureSessionStatus.Draft;

    public Course Course { get; set; } = null!;

    public ICollection<AttendanceRecord> AttendanceRecords { get; } = new List<AttendanceRecord>();
}