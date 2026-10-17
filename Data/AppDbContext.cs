using Microsoft.EntityFrameworkCore;
using UniversityDbConsole.Models;

namespace UniversityDbConsole.Data;

public class AppDbContext : DbContext
{
    public DbSet<Faculty> Faculties { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<Major> Majors { get; set; }
    public DbSet<StudyGroup> StudyGroups { get; set; }
    public DbSet<Semester> Semesters { get; set; }
    public DbSet<AuditoriumType> AuditoriumTypes { get; set; }
    public DbSet<Auditorium> Auditoriums { get; set; }
    public DbSet<ExamType> ExamTypes { get; set; }
    public DbSet<UserAccount> UserAccounts { get; set; }
    public DbSet<Student> Students { get; set; }
    public DbSet<Teacher> Teachers { get; set; }
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<Schedule> Schedules { get; set; }
    public DbSet<Grade> Grades { get; set; }
    public DbSet<Attendance> Attendances { get; set; }

    public DbSet<StudentGradeDto> StudentGradesView { get; set; }
    public DbSet<ActiveUserDto> ActiveUsersView { get; set; }
    public DbSet<ActiveSubjectDto> ActiveSubjectsView { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
        .UseNpgsql("Host=localhost;Port=5432;Database=universitydb;Username=username_placeholder;Password=password_placeholder")
        .UseLowerCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StudentGradeDto>().HasNoKey().ToView("v_studentgrades");
        modelBuilder.Entity<ActiveUserDto>().HasNoKey().ToView("v_activeusers");
        modelBuilder.Entity<ActiveSubjectDto>().HasNoKey().ToView("v_activesubjects");
        
        modelBuilder.Entity<Student>().ToView("v_student");
        modelBuilder.Entity<StudyGroup>().ToView("v_studygroup");
        modelBuilder.Entity<Subject>().ToView("v_subject");
        modelBuilder.Entity<Teacher>().ToView("v_teacher");
        modelBuilder.Entity<Semester>().ToView("v_semester");
        modelBuilder.Entity<ExamType>().ToView("v_examtype");
        modelBuilder.Entity<Department>().ToView("v_department");
        modelBuilder.Entity<UserAccount>().ToView("v_useraccount");
        modelBuilder.Entity<Grade>().ToView("v_grade");
        
    }
}