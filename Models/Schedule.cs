namespace UniversityDbConsole.Models;

public class Schedule
{
    public int Id { get; set; }
    public int StudyGroupId { get; set; }
    public int SubjectId { get; set; }
    public int TeacherId { get; set; }
    public int AuditoriumId { get; set; }
    public int SemesterId { get; set; }
    public DateTime LessonTime { get; set; }
}
