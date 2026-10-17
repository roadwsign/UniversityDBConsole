using System.ComponentModel.DataAnnotations;

namespace UniversityDbConsole.Models;

public class Student
{
    [Key]
    public int UserId { get; set; }
    public int StudyGroupId { get; set; }
}