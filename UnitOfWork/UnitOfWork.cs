using Microsoft.EntityFrameworkCore.Storage;
using UniversityDbConsole.Data;
using UniversityDbConsole.Models;
using UniversityDbConsole.Repositories;

namespace UniversityDbConsole.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
    IDbRoutinesRepository Routines { get; }
    
    IGenericRepository<Faculty> Faculties { get; }
    IGenericRepository<Department> Departments { get; }
    IGenericRepository<Major> Majors { get; }
    IGenericRepository<StudyGroup> StudyGroups { get; }
    IGenericRepository<Semester> Semesters { get; }
    IGenericRepository<AuditoriumType> AuditoriumTypes { get; }
    IGenericRepository<Auditorium> Auditoriums { get; }
    IGenericRepository<ExamType> ExamTypes { get; }
    IGenericRepository<UserAccount> UserAccounts { get; }
    IGenericRepository<Student> Students { get; }
    IGenericRepository<Teacher> Teachers { get; }
    IGenericRepository<Subject> Subjects { get; }
    IGenericRepository<Schedule> Schedules { get; }
    IGenericRepository<Grade> Grades { get; }
    IGenericRepository<Attendance> Attendances { get; }

    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    public IDbRoutinesRepository Routines { get; private set; }
    public IGenericRepository<Faculty> Faculties { get; private set; }
    public IGenericRepository<Department> Departments { get; private set; }
    public IGenericRepository<Major> Majors { get; private set; }
    public IGenericRepository<StudyGroup> StudyGroups { get; private set; }
    public IGenericRepository<Semester> Semesters { get; private set; }
    public IGenericRepository<AuditoriumType> AuditoriumTypes { get; private set; }
    public IGenericRepository<Auditorium> Auditoriums { get; private set; }
    public IGenericRepository<ExamType> ExamTypes { get; private set; }
    public IGenericRepository<UserAccount> UserAccounts { get; private set; }
    public IGenericRepository<Student> Students { get; private set; }
    public IGenericRepository<Teacher> Teachers { get; private set; }
    public IGenericRepository<Subject> Subjects { get; private set; }
    public IGenericRepository<Schedule> Schedules { get; private set; }
    public IGenericRepository<Grade> Grades { get; private set; }
    public IGenericRepository<Attendance> Attendances { get; private set; }

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
        Routines = new DbRoutinesRepository(_context);
        Faculties = new GenericRepository<Faculty>(_context);
        Departments = new GenericRepository<Department>(_context);
        Majors = new GenericRepository<Major>(_context);
        StudyGroups = new GenericRepository<StudyGroup>(_context);
        Semesters = new GenericRepository<Semester>(_context);
        AuditoriumTypes = new GenericRepository<AuditoriumType>(_context);
        Auditoriums = new GenericRepository<Auditorium>(_context);
        ExamTypes = new GenericRepository<ExamType>(_context);
        UserAccounts = new GenericRepository<UserAccount>(_context);
        Students = new GenericRepository<Student>(_context);
        Teachers = new GenericRepository<Teacher>(_context);
        Subjects = new GenericRepository<Subject>(_context);
        Schedules = new GenericRepository<Schedule>(_context);
        Grades = new GenericRepository<Grade>(_context);
        Attendances = new GenericRepository<Attendance>(_context);
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }
    
    public async Task CommitTransactionAsync()
    {
        try 
        { 
            await _context.SaveChangesAsync(); 
            if (_transaction != null) 
                await _transaction.CommitAsync(); 
        }
        catch 
        { 
            await RollbackTransactionAsync(); 
            throw; 
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null) 
        { 
            await _transaction.RollbackAsync(); 
            await _transaction.DisposeAsync(); 
            _transaction = null; 
        }
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}