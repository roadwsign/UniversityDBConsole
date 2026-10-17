namespace UniversityDbConsole.Models;

public class StudyGroup
{
    public int Id { get; set; }
    public int MajorId { get; set; }
    public string Name { get; set; } = null!;
}
