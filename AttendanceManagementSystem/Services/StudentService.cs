using AttendanceManagementSystem.Data;
using AttendanceManagementSystem.Interfaces;
using AttendanceManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceManagementSystem.Services;

public sealed class StudentService(AttendanceDbContext dbContext) : IStudentService
{
    public async Task<IReadOnlyList<Student>> GetStudentsAsync(
        int courseId,
        string? searchText = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Students
            .AsNoTracking()
            .Where(student => student.CourseId == courseId);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var search = searchText.Trim();
            query = query.Where(student =>
                student.UniversityId.Contains(search) ||
                student.FullName.Contains(search));
        }

        return await query
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.UniversityId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Student?> UpdateFullNameAsync(
        int studentId,
        string fullName,
        CancellationToken cancellationToken = default)
    {
        var trimmedName = fullName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new ArgumentException("Full name is required.", nameof(fullName));
        }

        var student = await dbContext.Students
            .SingleOrDefaultAsync(existing => existing.Id == studentId, cancellationToken);

        if (student is null)
        {
            return null;
        }

        student.FullName = trimmedName;
        await dbContext.SaveChangesAsync(cancellationToken);
        return student;
    }

    public async Task<bool> DeactivateAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var student = await dbContext.Students
            .SingleOrDefaultAsync(existing => existing.Id == studentId, cancellationToken);

        if (student is null || !student.IsActive)
        {
            return false;
        }

        student.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<StudentImportSummary> ImportAsync(
        int courseId,
        IReadOnlyList<CsvStudentRow> rows,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
        {
            return new StudentImportSummary();
        }

        var universityIds = rows
            .Select(row => row.UniversityId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var existingStudents = await dbContext.Students
            .Where(student => universityIds.Contains(student.UniversityId))
            .ToListAsync(cancellationToken);
        var existingByUniversityId = existingStudents.ToDictionary(
            student => student.UniversityId,
            StringComparer.OrdinalIgnoreCase);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var errors = new List<CsvRowError>();
        var added = 0;
        var updated = 0;

        foreach (var row in rows)
        {
            if (existingByUniversityId.TryGetValue(row.UniversityId, out var existingStudent))
            {
                if (existingStudent.CourseId != courseId)
                {
                    errors.Add(new CsvRowError(
                        row.RowNumber,
                        $"UniversityId '{row.UniversityId}' belongs to another course and was skipped."));
                    continue;
                }

                existingStudent.FullName = row.FullName;
                updated++;
                continue;
            }

            var newStudent = new Student
            {
                UniversityId = row.UniversityId,
                FullName = row.FullName,
                IsActive = true,
                CourseId = courseId
            };

            dbContext.Students.Add(newStudent);
            existingByUniversityId.Add(row.UniversityId, newStudent);
            added++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new StudentImportSummary
        {
            NewStudentsAdded = added,
            ExistingStudentsUpdated = updated,
            InvalidOrSkippedRows = errors.Count,
            Errors = errors
        };
    }
}
