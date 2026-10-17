using Microsoft.EntityFrameworkCore;
using UniversityDbConsole.Data;
using UniversityDbConsole.Models;

namespace UniversityDbConsole.Repositories;

public interface IGenericRepository<T> where T : class
{
    Task<List<T>> GetAllAsync();
    Task AddAsync(T entity);
}

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    private readonly AppDbContext _context;
    public GenericRepository(AppDbContext context) { _context = context; }

    public async Task<List<T>> GetAllAsync() => await _context.Set<T>().ToListAsync();
    public async Task AddAsync(T entity) => await _context.Set<T>().AddAsync(entity);
}

public interface IDbRoutinesRepository
{
    Task CreateStudentProfileAsync(string name, string email, string hash, int groupId);
    Task CreateTeacherProfileAsync(string name, string email, string hash, int depId);
    Task AssignGradeAsync(int studentId, int subjectId, int teacherId, int semesterId, int examTypeId, int score, int modifiedBy);
    Task SoftDeleteUserAsync(int userId, int modifiedBy);
    
    Task<List<StudentGradeDto>> GetStudentGradesAsync(string studentName);
    Task<List<ActiveUserDto>> GetActiveUsersAsync();
    Task<List<ActiveSubjectDto>> GetActiveSubjectsAsync();
}

public class DbRoutinesRepository : IDbRoutinesRepository
{
    private readonly AppDbContext _context;
    public DbRoutinesRepository(AppDbContext context) { _context = context; }

    public async Task CreateStudentProfileAsync(string name, string email, string hash, int groupId) =>
        await _context.Database.ExecuteSqlRawAsync("CALL sp_CreateStudentProfile({0}, {1}, {2}, {3})", name, email, hash, groupId);

    public async Task CreateTeacherProfileAsync(string name, string email, string hash, int depId) =>
        await _context.Database.ExecuteSqlRawAsync("CALL sp_CreateTeacherProfile({0}, {1}, {2}, {3})", name, email, hash, depId);

    public async Task AssignGradeAsync(int studentId, int subjectId, int teacherId, int semesterId, int examTypeId, int score, int modifiedBy) =>
        await _context.Database.ExecuteSqlRawAsync("CALL sp_AssignGrade({0}, {1}, {2}, {3}, {4}, {5}, {6})", studentId, subjectId, teacherId, semesterId, examTypeId, score, modifiedBy);

    public async Task SoftDeleteUserAsync(int userId, int modifiedBy) =>
        await _context.Database.ExecuteSqlRawAsync("CALL sp_SoftDeleteUser({0}, {1})", userId, modifiedBy);

    public async Task<List<StudentGradeDto>> GetStudentGradesAsync(string name) =>
        await _context.StudentGradesView.Where(g => g.StudentName == name).ToListAsync();

    public async Task<List<ActiveUserDto>> GetActiveUsersAsync() =>
        await _context.ActiveUsersView.ToListAsync();

    public async Task<List<ActiveSubjectDto>> GetActiveSubjectsAsync() =>
        await _context.Set<ActiveSubjectDto>().ToListAsync();
}