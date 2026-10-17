namespace UniversityDbConsole.Models;

public class StudentGradeDto
{
    public int GradeId { get; set; }
    public string StudentName { get; set; } = null!;
    public string SubjectName { get; set; } = null!;
    public string ExamType { get; set; } = null!;
    public int Score { get; set; }
}