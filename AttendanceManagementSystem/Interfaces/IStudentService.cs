using AttendanceManagementSystem.Models;
using AttendanceManagementSystem.Services;

namespace AttendanceManagementSystem.Interfaces;

public interface IStudentService
{
    Task<IReadOnlyList<Student>> GetStudentsAsync(int courseId, string? searchText = null, CancellationToken cancellationToken = default);

    Task<Student?> UpdateFullNameAsync(int studentId, string fullName, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(int studentId, CancellationToken cancellationToken = default);

    Task<StudentImportSummary> ImportAsync(int courseId, IReadOnlyList<CsvStudentRow> rows, CancellationToken cancellationToken = default);
}