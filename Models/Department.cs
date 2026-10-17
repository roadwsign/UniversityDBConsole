namespace UniversityDbConsole.Models;

public class Department
{
    public int Id { get; set; }
    public int FacultyId { get; set; }
    public string Name { get; set; } = null!;
}
