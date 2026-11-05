# Attendance Management System

## Purpose

Attendance Management System is a local desktop application for one instructor to manage attendance for one course or subject. Phase 4 adds the initial course setup workflow and a placeholder dashboard while keeping all attendance workflows deferred.

## Technology stack

- C#
- .NET 10 (`net10.0-windows`)
- WPF with XAML
- MVVM using lightweight, in-project view models
- .NET Generic Host and `Microsoft.Extensions.Hosting` for application startup and dependency injection
- Entity Framework Core 10.0.5
- SQL Server through `Microsoft.EntityFrameworkCore.SqlServer` 10.0.5
- EF Core migrations through `Microsoft.EntityFrameworkCore.Design` 10.0.5

## Architecture and folders

The solution intentionally contains one application project. Separate Domain, Application, Infrastructure, and test projects are not being introduced at this stage.

- `Models` - application data models when they are needed
- `Views` - WPF windows and other XAML presentation elements
- `ViewModels` - UI state and presentation logic exposed to views
- `Services` - concrete application services, including single-course persistence
- `Interfaces` - small abstractions for service boundaries, including `ICourseService`
- `Data` - EF Core context and persistence-related code
- `Resources` - shared WPF resources, styles, and other presentation assets
- `Migrations` - EF Core migration classes and the model snapshot

The current UI shell consists of `Views/MainWindow.xaml` and `ViewModels/MainViewModel.cs`. The database model contains the core course, student, lecture session, and attendance record entities. Phase 4 adds `CourseSetupView` and `DashboardView`; student and attendance workflows remain deferred.

## MVVM and dependency injection

Presentation remains in XAML. The main window receives its `MainViewModel` through its constructor and binds to the view model rather than containing application logic. Code-behind is limited to essential WPF initialization and assigning the injected data context.

`App.xaml.cs` is the composition root. It creates a .NET host and centrally registers `AttendanceDbContext`, `ICourseService`, `CourseSetupViewModel`, `DashboardViewModel`, `MainViewModel`, and `MainWindow`. A retained application scope keeps the scoped EF Core context and course service available while the window is open. The context receives its SQL Server options through DI and reads the `DefaultConnection` value from application configuration.

## Phase 4 course setup

After migrations are applied, `MainViewModel` asks `ICourseService` whether the single supported course exists. A missing course opens `CourseSetupView`; an existing course opens `DashboardView`. The initial form requires Course Name, Instructor Name, Academic Year, and Semester. Semester is selected from Fall, Spring, or Summer. Whitespace-only values are rejected with field-level validation messages, and the asynchronous Save command prevents repeated submissions while persistence is in progress.

The course service updates the existing course when one is present and creates one only when the database has no course. Saving the initial course navigates to the dashboard. The dashboard displays all four course values and provides an Edit Course action. Editing loads the existing values, updates the same record, and supports Cancel without saving. No database migration was required for Phase 4.

## Database configuration

The connection string is stored in `AttendanceManagementSystem/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=AttendanceManagementSystem;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

`appsettings.json` is copied to the application output directory and is not hardcoded in C# code. `Data/AttendanceDbContext.cs` contains the dedicated context. `Data/AttendanceDbContextFactory.cs` provides design-time context creation for EF Core tooling using the same configured connection string.

The application uses Windows Authentication. SQL Server must be installed and accessible on the target computer, and the Windows account running the application must have sufficient permissions to connect to the server and create/update the `AttendanceManagementSystem` database.

## Migrations and automatic initialization

Migrations are the only database schema management mechanism. The initial migration is:

- `20261101022514_InitialCreate`
- `Migrations/20261101022514_InitialCreate.cs`
- `Migrations/20261101022514_InitialCreate.Designer.cs`
- `Migrations/AttendanceDbContextModelSnapshot.cs`

Because the current model has no business entities, the migration has empty `Up` and `Down` methods and creates no business tables. It is still a valid baseline migration and records the model state for future changes.

To create a migration from the project directory:

```powershell
dotnet ef migrations add <MigrationName> --project AttendanceManagementSystem.csproj
```

To apply pending migrations:

```powershell
dotnet ef database update --project AttendanceManagementSystem.csproj
```

At application startup, the host is initialized first, an `AttendanceDbContext` is resolved through DI, and `Database.MigrateAsync()` runs once before the main window is displayed. If connection or migration initialization fails, a clear WPF error message is shown and the application shuts down instead of opening in a misleading state. The implementation does not use `EnsureCreated`, `EnsureCreatedAsync`, `EnsureDeleted`, or manual SQL table scripts.

The configured local `AttendanceManagementSystem` database was upgraded with the Phase 3 migration without a destructive reset. The existing Phase 2 migration remains unchanged.

## Phase 3 core database design

`Data/AttendanceDbContext.cs` exposes `DbSet` properties for `Course`, `Student`, `LectureSession`, and `AttendanceRecord`. Fluent configuration defines required fields, maximum string lengths, enum storage, defaults, indexes, foreign keys, and restrictive delete behavior.

- `Course` stores the course name, instructor name, academic year, and semester.
- `Student` stores a unique university ID, full name, active state, and required course relationship. `IsActive` defaults to `true`.
- `LectureSession` stores its course, lecture date, optional title, and `LectureSessionStatus`, which defaults to `Draft` and supports `Draft` and `Completed`.
- `AttendanceRecord` stores its session, student, and nullable `AttendanceStatus`. The enum supports only `Present` and `Absent`; `null` means not yet marked.

`Student.UniversityId` has a unique index. `AttendanceRecord` has a unique composite index on `LectureSessionId` and `StudentId`, allowing only one record per student/session pair. Course-to-student, course-to-session, session-to-record, and student-to-record foreign keys use restrictive delete behavior so deleting a principal cannot cascade-delete historical attendance records.

When a lecture session is created in a later application phase, one attendance record will be created for each currently active student in the course. This captures the session roster at creation time. Students added later will not be added retroactively to existing sessions, and deactivated students retain their historical attendance records. Attendance starts with a null status and must eventually be explicitly marked Present or Absent. Completed sessions will be locked by application logic in a later phase; the current phase only stores the status value.

## Development workflow

Implement features incrementally. Build and verify the solution after each phase before starting the next one. Prefer the simplest design that meets the current requirements, and avoid unnecessary frameworks, abstractions, packages, and architectural complexity. Keep new code testable even though automated tests are deferred for now.

For database changes, create a migration, inspect it, apply it to the development database, and verify `__EFMigrationsHistory` before continuing. Do not manually edit migration history or use destructive reset commands without explicit approval.

## Agreed business decisions

- One instructor uses the application locally.
- The initial version manages one course/subject.
- Students do not log in or use the application.
- Students are imported from CSV.
- Re-importing CSV updates existing students by university ID and adds new students.
- Student university IDs must be unique.
- Students can be deactivated without deleting historical attendance records.
- A newly added student starts appearing in newly created sessions only.
- Sessions are created manually and their number is not fixed at 15.
- Attendance states are Present and Absent only.
- Attendance has no default state.
- A session cannot be completed until all students assigned to it have an attendance state.
- Completed sessions are locked and attendance cannot be edited.
- Absence warnings appear at four absences in completed sessions.

## Current phase and limitations

Phase 4 is complete for course setup. The verified implementation includes startup routing after migrations, single-course persistence, setup validation, duplicate prevention through update-or-create logic, edit/cancel navigation, and a placeholder dashboard.

The following remain deferred:

- CSV student import and update behavior
- Student and course CRUD workflows
- Lecture session creation workflow
- Attendance record creation workflow
- Attendance business logic
- Session completion and locking rules
- Absence warnings
- PDF reports
- Full dashboard or final UI design
- Automated test projects
- Authentication or user management
- Backup and restore functionality
- Separate class-library projects

Course setup is intentionally limited to one course. It does not provide multiple-course management, student management, session creation, attendance marking, statistics, or other dashboard features.

When the application is moved to another computer, install and configure SQL Server, ensure the target Windows account has the required permissions, review `appsettings.json`, and run the application or apply migrations with EF tooling. Database files and local machine-specific settings are not included in the source code, and the existing DVLD database is not used or transferred.

No Phase 5 features should be implemented until explicitly requested.
