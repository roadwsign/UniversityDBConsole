namespace UniversityDbConsole.Models;

public class Auditorium
{
    public int Id { get; set; }
    public int AuditoriumTypeId { get; set; }
    public string? BuildingNumber { get; set; }
    public string RoomNumber { get; set; } = null!;
    public int? Capacity { get; set; }
}
