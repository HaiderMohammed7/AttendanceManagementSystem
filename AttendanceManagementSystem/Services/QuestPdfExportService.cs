using System.Globalization;
using System.IO;
using AttendanceManagementSystem.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AttendanceManagementSystem.Services;

public sealed class QuestPdfExportService : IPdfExportService
{
    private static readonly string? ArabicFontPath = FindArabicFont();

    public QuestPdfExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        if (ArabicFontPath is not null)
        {
            using var fontStream = File.OpenRead(ArabicFontPath);
            QuestPDF.Drawing.FontManager.RegisterFont(fontStream);
        }
    }

    public Task ExportCourseReportAsync(CourseReportData report, string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BuildCourseDocument(report).GeneratePdf(filePath);
        return Task.CompletedTask;
    }

    public Task ExportStudentReportAsync(StudentReportData report, string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BuildStudentDocument(report).GeneratePdf(filePath);
        return Task.CompletedTask;
    }

    public Task ExportWarningsReportAsync(WarningsReportData report, string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BuildWarningsDocument(report).GeneratePdf(filePath);
        return Task.CompletedTask;
    }

    private static Document BuildCourseDocument(CourseReportData report) =>
        Document.Create(container => container.Page(page =>
        {
            ConfigurePage(page, "Course Attendance Report", report.Course, report.GeneratedAt);
            page.Content().Column(column =>
            {
                Summary(column, $"Total Students: {report.TotalStudents}", $"Active Students: {report.ActiveStudents}", $"Inactive Students: {report.InactiveStudents}", $"Completed Lectures: {report.CompletedLectures}", $"Total Present: {report.TotalPresent}", $"Total Absent: {report.TotalAbsent}", $"Students with Warnings: {report.ActiveStudentsWithWarnings}");
                column.Item().PaddingTop(14).Element(container =>
                    Table(container, ["University ID", "Full Name", "Present", "Absent", "Attendance", "Warning"], report.Students.Select(student => new[] { student.UniversityId, student.FullName, student.PresentCount.ToString(), student.AbsentCount.ToString(), student.AttendancePercentage, student.WarningStatus })));
            });
        }));

    private static Document BuildStudentDocument(StudentReportData report) =>
        Document.Create(container => container.Page(page =>
        {
            ConfigurePage(page, "Individual Student Report", report.Course, report.GeneratedAt);
            page.Content().Column(column =>
            {
                Summary(column, $"University ID: {report.UniversityId}", $"Full Name: {report.FullName}", $"Student Status: {(report.IsActive ? "Active" : "Inactive")}", $"Present Count: {report.PresentCount}", $"Absent Count: {report.AbsentCount}", $"Attendance: {report.AttendancePercentage}", $"Absence: {report.AbsencePercentage}", $"Warning Status: {report.WarningStatus}");
                column.Item().PaddingTop(14).Element(container =>
                    Table(container, ["Lecture Date", "Lecture Title", "Attendance"], report.History.Select(item => new[] { item.LectureDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), item.Title, item.Status.ToString() })));
            });
        }));

    private static Document BuildWarningsDocument(WarningsReportData report) =>
        Document.Create(container => container.Page(page =>
        {
            ConfigurePage(page, "Students With Warnings Report", report.Course, report.GeneratedAt);
            page.Content().Column(column =>
            {
                if (report.Students.Count == 0)
                {
                    column.Item().PaddingTop(20).Text("No students with warnings").FontSize(14);
                    return;
                }

                column.Item().PaddingTop(12).Element(container =>
                    Table(container, ["University ID", "Full Name", "Absences", "Attendance", "Warning"], report.Students.Select(student => new[] { student.UniversityId, student.FullName, student.AbsenceCount.ToString(), student.AttendancePercentage, student.WarningStatus })));
            });
        }));

    private static void ConfigurePage(PageDescriptor page, string title, ReportCourseInfo course, DateTime generatedAt)
    {
        page.Size(PageSizes.A4.Landscape());
        page.Margin(32);
        page.DefaultTextStyle(style => style.FontFamily(ArabicFontPath is null ? "Arial" : "Arial").FontSize(9));
        page.Header().Column(column =>
        {
            column.Item().Text(title).Bold().FontSize(20).FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);
            column.Item().PaddingTop(6).Text($"Course: {course.CourseName} | Instructor: {course.InstructorName}");
            column.Item().Text($"Academic Year: {course.AcademicYear} | Semester: {course.Semester}");
            column.Item().Text($"Generated: {generatedAt:yyyy-MM-dd HH:mm}").FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
        });
        page.Footer().AlignCenter().Text(text =>
        {
            text.Span("Page ");
            text.CurrentPageNumber();
            text.Span(" of ");
            text.TotalPages();
        });
    }

    private static void Summary(ColumnDescriptor column, params string[] lines)
    {
        column.Item().PaddingTop(14).Grid(grid =>
        {
            grid.Columns(3);
            foreach (var line in lines)
            {
                grid.Item().Padding(4).Text(line);
            }
        });
    }

    private static void Table(IContainer container, string[] headers, IEnumerable<string[]> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var _ in headers) columns.RelativeColumn();
            });
            table.Header(header =>
            {
                foreach (var title in headers)
                {
                    header.Cell().Background(QuestPDF.Helpers.Colors.Blue.Darken2).Padding(5).Text(title).FontColor(QuestPDF.Helpers.Colors.White).Bold();
                }
            });
            foreach (var row in rows)
            {
                foreach (var value in row)
                {
                    table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(5).Text(value);
                }
            }
        });
    }

    private static string? FindArabicFont()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "tahoma.ttf"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf")
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
