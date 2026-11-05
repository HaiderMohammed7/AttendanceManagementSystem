namespace AttendanceManagementSystem.Services;

public sealed record CsvStudentRow(int RowNumber, string UniversityId, string FullName);

public sealed record CsvRowError(int RowNumber, string Message);

public sealed class CsvParseResult
{
    public IReadOnlyList<CsvStudentRow> Rows { get; init; } = [];

    public IReadOnlyList<CsvRowError> Errors { get; init; } = [];
}

public sealed class StudentImportSummary
{
    public int NewStudentsAdded { get; init; }

    public int ExistingStudentsUpdated { get; init; }

    public int InvalidOrSkippedRows { get; init; }

    public IReadOnlyList<CsvRowError> Errors { get; init; } = [];
}
