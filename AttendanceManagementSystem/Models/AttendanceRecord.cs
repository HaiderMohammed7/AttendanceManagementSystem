namespace AttendanceManagementSystem.Models;

public sealed class AttendanceRecord
{
    public int Id { get; set; }

    public int LectureSessionId { get; set; }

    public int StudentId { get; set; }

    public AttendanceStatus? Status { get; set; }

    public LectureSession LectureSession { get; set; } = null!;

    public Student Student { get; set; } = null!;
}