namespace UniversityDbConsole.NoSql.Models;

public class ScheduleCacheItem
{
    public int ScheduleId { get; set; }
    public DateTime LessonTime { get; set; }
    public int StudyGroupId { get; set; }
    public string StudyGroupName { get; set; } = string.Empty;
    public int SemesterId { get; set; }
    public string AcademicYear { get; set; } = string.Empty;
    public int SemesterNumber { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string AuditoriumType { get; set; } = string.Empty;
    public string BuildingNumber { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
}
