using System.ComponentModel.DataAnnotations;

namespace UniversityDbConsole.Models;

public class Teacher
{
    [Key]
    public int UserId { get; set; }
    public int DepartmentId { get; set; }
}
