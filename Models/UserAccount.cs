namespace UniversityDbConsole.Models;

public class UserAccount
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public bool IsDeleted { get; set; }
    public DateTime LastModifiedDate { get; set; }
    public int? LastModifiedBy { get; set; }
}