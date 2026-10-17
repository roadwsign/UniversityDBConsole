namespace UniversityDbConsole.Models;

public class Grade
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int SubjectId { get; set; }
    public int TeacherId { get; set; }
    public int SemesterId { get; set; }
    public int ExamTypeId { get; set; }
    public int Score { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public int? LastModifiedBy { get; set; }
}
