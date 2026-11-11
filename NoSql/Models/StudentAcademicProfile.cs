using MongoDB.Bson.Serialization.Attributes;

namespace UniversityDbConsole.NoSql.Models;

public class StudentAcademicProfile
{
    [BsonId]
    public int StudentId { get; set; }

    public string StudentName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public StudyGroupInfo Group { get; set; } = new();
    public List<AcademicSemester> Semesters { get; set; } = new();
}

public class StudyGroupInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class AcademicSemester
{
    public int SemesterId { get; set; }
    public string AcademicYear { get; set; } = string.Empty;
    public int SemesterNumber { get; set; }
    public List<AcademicGrade> Grades { get; set; } = new();
    public List<SubjectAttendanceSummary> Attendance { get; set; } = new();
}

public class AcademicGrade
{
    public int GradeId { get; set; }
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string ExamType { get; set; } = string.Empty;
    public int Score { get; set; }
}

public class SubjectAttendanceSummary
{
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int TotalClasses { get; set; }
    public int AttendedClasses { get; set; }
    public int MissedClasses { get; set; }
}
