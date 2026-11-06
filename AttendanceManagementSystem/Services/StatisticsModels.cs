using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Services;

public sealed record DashboardStatistics(
    int ActiveStudents,
    int CompletedLectures,
    int DraftLectures,
    int TotalPresent,
    int TotalAbsent,
    int StudentsWithWarnings,
    IReadOnlyList<StudentWarning> Warnings);

public sealed record StudentWarning(
    int StudentId,
    string UniversityId,
    string FullName,
    int AbsenceCount);

public sealed record StudentAttendanceStatistics(
    int PresentCount,
    int AbsentCount)
{
    public int TotalCompletedAttendance => PresentCount + AbsentCount;
    public string AttendancePercentage => TotalCompletedAttendance == 0 ? "N/A" : $"{PresentCount * 100d / TotalCompletedAttendance:F2}%";
    public string AbsencePercentage => TotalCompletedAttendance == 0 ? "N/A" : $"{AbsentCount * 100d / TotalCompletedAttendance:F2}%";
    public string WarningStatus => AbsentCount >= 3 ? "Warning" : "No Warning";
}

public sealed record StudentAttendanceHistoryItem(
    DateTime LectureDate,
    string Title,
    LectureSessionStatus SessionStatus,
    AttendanceStatus? AttendanceStatus)
{
    public string AttendanceStatusText => AttendanceStatus?.ToString() ?? "Not Marked";
}