namespace UniversityDbConsole.Models;

public class Attendance
{
    public int Id { get; set; }
    public int ScheduleId { get; set; }
    public int StudentId { get; set; }
    public bool IsPresent { get; set; }
}
