using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Services;

public sealed record ReportCourseInfo(
    string CourseName,
    string InstructorName,
    string AcademicYear,
    string Semester);

public sealed record ReportStudentAttendance(
    string UniversityId,
    string FullName,
    bool IsActive,
    int PresentCount,
    int AbsentCount)
{
    public string AttendancePercentage => PresentCount + AbsentCount == 0 ? "N/A" : $"{PresentCount * 100d / (PresentCount + AbsentCount):F2}%";
    public string AbsencePercentage => PresentCount + AbsentCount == 0 ? "N/A" : $"{AbsentCount * 100d / (PresentCount + AbsentCount):F2}%";
    public string WarningStatus => AbsentCount >= 3 ? "Warning" : "No Warning";
}

public sealed record CourseReportData(
    ReportCourseInfo Course,
    DateTime GeneratedAt,
    int TotalStudents,
    int ActiveStudents,
    int InactiveStudents,
    int CompletedLectures,
    int TotalPresent,
    int TotalAbsent,
    int ActiveStudentsWithWarnings,
    IReadOnlyList<ReportStudentAttendance> Students);

public sealed record StudentReportHistory(
    DateTime LectureDate,
    string Title,
    AttendanceStatus Status);

public sealed record StudentReportData(
    ReportCourseInfo Course,
    DateTime GeneratedAt,
    string UniversityId,
    string FullName,
    bool IsActive,
    int PresentCount,
    int AbsentCount,
    IReadOnlyList<StudentReportHistory> History)
{
    public string AttendancePercentage => PresentCount + AbsentCount == 0 ? "N/A" : $"{PresentCount * 100d / (PresentCount + AbsentCount):F2}%";
    public string AbsencePercentage => PresentCount + AbsentCount == 0 ? "N/A" : $"{AbsentCount * 100d / (PresentCount + AbsentCount):F2}%";
    public string WarningStatus => AbsentCount >= 3 ? "Warning" : "No Warning";
}

public sealed record WarningReportStudent(
    string UniversityId,
    string FullName,
    int AbsenceCount,
    int PresentCount)
{
    public string AttendancePercentage => PresentCount + AbsenceCount == 0 ? "N/A" : $"{PresentCount * 100d / (PresentCount + AbsenceCount):F2}%";
    public string WarningStatus => "Warning";
}

public sealed record WarningsReportData(
    ReportCourseInfo Course,
    DateTime GeneratedAt,
    IReadOnlyList<WarningReportStudent> Students);
