namespace AttendanceManagementSystem.Models;

public sealed class Course
{
    public int Id { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public string InstructorName { get; set; } = string.Empty;

    public string AcademicYear { get; set; } = string.Empty;

    public string Semester { get; set; } = string.Empty;

    public ICollection<Student> Students { get; } = new List<Student>();

    public ICollection<LectureSession> LectureSessions { get; } = new List<LectureSession>();
}