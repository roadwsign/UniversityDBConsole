using UniversityDbConsole.Data;
using UniversityDbConsole.UnitOfWork;

using var context = new AppDbContext();
using var unitOfWork = new UnitOfWork(context);

var groups = await unitOfWork.StudyGroups.GetAllAsync();
var subjects = await unitOfWork.Subjects.GetAllAsync();
var teachers = await unitOfWork.Teachers.GetAllAsync();
var semesters = await unitOfWork.Semesters.GetAllAsync();
var examTypes = await unitOfWork.ExamTypes.GetAllAsync();
var departments = await unitOfWork.Departments.GetAllAsync();

if (groups.Count == 0 || subjects.Count == 0 || teachers.Count == 0 || semesters.Count == 0 || examTypes.Count == 0 || departments.Count == 0)
{
    Console.WriteLine("База даних порожня. Заповніть довідники.");
    return;
}

var group = groups.First();
var subject = subjects.First();
var teacher = teachers.First();
var semester = semesters.First();
var examType = examTypes.First();
var department = departments.First();

string time = DateTime.Now.ToString("HHmmss");
string testStudentName = $"Студент {time}";
string testStudentEmail = $"student_{time}@gmail.com";
string testTeacherName = $"Викладач {time}";
string testTeacherEmail = $"teacher_{time}@gmail.com";

try
{
    await unitOfWork.BeginTransactionAsync();
    await unitOfWork.Routines.CreateStudentProfileAsync(testStudentName, testStudentEmail, "pass123", group.Id);
    await unitOfWork.CommitTransactionAsync();
    Console.WriteLine("Тест 1: Студента створено (sp_CreateStudentProfile)");
    
    var allUsers = await unitOfWork.UserAccounts.GetAllAsync();
    var createdUser = allUsers.First(u => u.Email == testStudentEmail);
    Console.WriteLine($"Стан БД (UserAccounts): ID: {createdUser.Id}, ПІБ: {createdUser.Name}, IsDeleted: {createdUser.IsDeleted}\n");

    await unitOfWork.BeginTransactionAsync();
    await unitOfWork.Routines.AssignGradeAsync(createdUser.Id, subject.Id, teacher.UserId, semester.Id, examType.Id, 95, teacher.UserId);
    await unitOfWork.CommitTransactionAsync();
    Console.WriteLine("Тест 2: Оцінку виставлено (sp_AssignGrade)");

    var allGrades = await unitOfWork.Grades.GetAllAsync();
    var dbGrade = allGrades.First(g => g.StudentId == createdUser.Id);
    Console.WriteLine($"Стан БД (Grades): ID: {dbGrade.Id}, Оцінка: {dbGrade.Score}, ID викладача: {dbGrade.TeacherId}\n");

    var gradesView = await unitOfWork.Routines.GetStudentGradesAsync(testStudentName);
    Console.WriteLine($"Тест 3: Читання з View (v_StudentGrades). Знайдено оцінок: {gradesView.Count}");
    foreach (var g in gradesView)
    {
        Console.WriteLine($"Вивід View: Предмет: {g.SubjectName}, Тип: {g.ExamType}, Оцінка: {g.Score}");
    }
    Console.WriteLine();
    
    var activeUsersBefore = await unitOfWork.Routines.GetActiveUsersAsync();
    Console.WriteLine($"Тест 4: Активні користувачі (v_ActiveUsers) до видалення: {activeUsersBefore.Count}\n");

    await unitOfWork.BeginTransactionAsync();
    await unitOfWork.Routines.SoftDeleteUserAsync(createdUser.Id, teacher.UserId);
    await unitOfWork.CommitTransactionAsync();
    Console.WriteLine("Тест 5: Користувача видалено (sp_SoftDeleteUser)");

    context.ChangeTracker.Clear();

    var usersAfterDelete = await unitOfWork.UserAccounts.GetAllAsync();
    var deletedUser = usersAfterDelete.First(u => u.Id == createdUser.Id);
    Console.WriteLine($"Стан БД (UserAccounts): ID: {deletedUser.Id}, IsDeleted: {deletedUser.IsDeleted}\n");

    var activeUsersAfter = await unitOfWork.Routines.GetActiveUsersAsync();
    Console.WriteLine($"Тест 6: Активні користувачі (v_ActiveUsers) після видалення: {activeUsersAfter.Count}\n");

    await unitOfWork.BeginTransactionAsync();
    await unitOfWork.Routines.CreateTeacherProfileAsync(testTeacherName, testTeacherEmail, "pass_tech_123", department.Id);
    await unitOfWork.CommitTransactionAsync();
    Console.WriteLine("Тест 7: Викладача створено (sp_CreateTeacherProfile)");

    var newUsers = await unitOfWork.UserAccounts.GetAllAsync();
    var createdTeacher = newUsers.First(u => u.Email == testTeacherEmail);
    Console.WriteLine($"Стан БД (UserAccounts): ID: {createdTeacher.Id}, ПІБ: {createdTeacher.Name}, IsDeleted: {createdTeacher.IsDeleted}\n");

    var activeSubjectsView = await unitOfWork.Routines.GetActiveSubjectsAsync();
    Console.WriteLine($"Тест 8: Читання з View (v_ActiveSubjects). Активних предметів у БД: {activeSubjectsView.Count}");
}
catch (Exception ex)
{
    await unitOfWork.RollbackTransactionAsync();
    Console.WriteLine($"Помилка: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Деталі: {ex.InnerException.Message}");
    }
}