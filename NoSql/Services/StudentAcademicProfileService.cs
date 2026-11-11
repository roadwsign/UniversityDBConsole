using MongoDB.Bson;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using UniversityDbConsole.NoSql.Models;
using UniversityDbConsole.NoSql.Data;

namespace UniversityDbConsole.NoSql.Services;

public class StudentAcademicProfileService
{
    private readonly NoSqlDbContext _context;
    private readonly IMongoCollection<StudentAcademicProfile> _profiles;

    public StudentAcademicProfileService(
        NoSqlDbContext context,
        IMongoCollection<StudentAcademicProfile> profiles)
    {
        _context = context;
        _profiles = profiles;
    }

    public async Task EnsureIndexesAsync()
    {
        var groupIndex = new CreateIndexModel<StudentAcademicProfile>(
            Builders<StudentAcademicProfile>.IndexKeys.Ascending(x => x.Group.Id));

        await _profiles.Indexes.CreateOneAsync(groupIndex);
    }

    public async Task<List<int>> GetStudentIdsAsync()
    {
        return await _context.Students
            .AsNoTracking()
            .Select(x => x.UserId)
            .ToListAsync();
    }

    public async Task<StudentAcademicProfile?> GetProfileFromPostgresSqlAsync(int studentId)
    {
        var student = await _context.Students
            .FromSqlInterpolated(
                $"SELECT * FROM v_student WHERE userid = {studentId}")
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (student == null)
            return null;

        var user = await _context.UserAccounts
            .FromSqlInterpolated(
                $"SELECT * FROM v_useraccount WHERE id = {student.UserId} AND NOT isdeleted")
            .AsNoTracking()
            .FirstOrDefaultAsync();

        var group = await _context.StudyGroups
            .FromSqlInterpolated(
                $"SELECT * FROM v_studygroup WHERE id = {student.StudyGroupId}")
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (user == null || group == null)
            return null;

        var grades = await _context.Grades
            .FromSqlInterpolated(
                $"SELECT * FROM v_grade WHERE studentid = {studentId}")
            .AsNoTracking()
            .ToListAsync();

        var attendances = await _context.Attendances
            .FromSqlInterpolated(
                $"SELECT * FROM attendances WHERE studentid = {studentId}")
            .AsNoTracking()
            .ToListAsync();

        var scheduleIds = attendances
            .Select(x => x.ScheduleId)
            .Distinct()
            .ToArray();

        var attendanceSchedules = scheduleIds.Length == 0
            ? new List<UniversityDbConsole.Models.Schedule>()
            : await _context.Schedules
                .FromSqlInterpolated(
                    $"SELECT * FROM schedules WHERE id = ANY({scheduleIds})")
                .AsNoTracking()
                .ToListAsync();

        var subjectIds = grades.Select(x => x.SubjectId)
            .Concat(attendanceSchedules.Select(x => x.SubjectId))
            .Distinct()
            .ToArray();

        var teacherIds = grades.Select(x => x.TeacherId)
            .Concat(attendanceSchedules.Select(x => x.TeacherId))
            .Distinct()
            .ToArray();

        var semesterIds = grades.Select(x => x.SemesterId)
            .Concat(attendanceSchedules.Select(x => x.SemesterId))
            .Distinct()
            .ToArray();

        var subjects = subjectIds.Length == 0
            ? new List<UniversityDbConsole.Models.Subject>()
            : await _context.Subjects
                .FromSqlInterpolated(
                    $"SELECT * FROM v_subject WHERE id = ANY({subjectIds}) AND NOT isdeleted")
                .AsNoTracking()
                .ToListAsync();

        var teachers = teacherIds.Length == 0
            ? new List<UniversityDbConsole.Models.Teacher>()
            : await _context.Teachers
                .FromSqlInterpolated(
                    $"SELECT * FROM v_teacher WHERE userid = ANY({teacherIds})")
                .AsNoTracking()
                .ToListAsync();

        var teacherUserIds = teachers
            .Select(x => x.UserId)
            .Distinct()
            .ToArray();

        var teacherUsers = teacherUserIds.Length == 0
            ? new List<UniversityDbConsole.Models.UserAccount>()
            : await _context.UserAccounts
                .FromSqlInterpolated(
                    $"SELECT * FROM v_useraccount WHERE id = ANY({teacherUserIds}) AND NOT isdeleted")
                .AsNoTracking()
                .ToListAsync();

        var semesters = semesterIds.Length == 0
            ? new List<UniversityDbConsole.Models.Semester>()
            : await _context.Semesters
                .FromSqlInterpolated(
                    $"SELECT * FROM v_semester WHERE id = ANY({semesterIds})")
                .AsNoTracking()
                .ToListAsync();

        var examTypeIds = grades
            .Select(x => x.ExamTypeId)
            .Distinct()
            .ToArray();

        var examTypes = examTypeIds.Length == 0
            ? new List<UniversityDbConsole.Models.ExamType>()
            : await _context.ExamTypes
                .FromSqlInterpolated(
                    $"SELECT * FROM v_examtype WHERE id = ANY({examTypeIds})")
                .AsNoTracking()
                .ToListAsync();

        var subjectById = subjects.ToDictionary(x => x.Id);

        var teacherNameById = teacherUsers
            .ToDictionary(x => x.Id, x => x.Name);

        var semesterById = semesters.ToDictionary(x => x.Id);

        var examTypeById = examTypes
            .ToDictionary(x => x.Id, x => x.Name);

        var scheduleById = attendanceSchedules
            .ToDictionary(x => x.Id);

        var academicSemesters = new List<AcademicSemester>();

        foreach (var semesterId in semesterIds)
        {
            if (!semesterById.TryGetValue(semesterId, out var semester))
                continue;

            var semesterGrades = grades
                .Where(x => x.SemesterId == semesterId)
                .Select(x => new AcademicGrade
                {
                    GradeId = x.Id,
                    SubjectId = x.SubjectId,
                    SubjectName = subjectById.TryGetValue(x.SubjectId, out var subject)
                        ? subject.Name
                        : "Невідома дисципліна",
                    TeacherId = x.TeacherId,
                    TeacherName = teacherNameById.TryGetValue(x.TeacherId, out var teacherName)
                        ? teacherName
                        : "Невідомий викладач",
                    ExamType = examTypeById.TryGetValue(x.ExamTypeId, out var examType)
                        ? examType
                        : "Невідомий тип контролю",
                    Score = x.Score
                })
                .ToList();

            var semesterAttendance = attendances
                .Where(x => scheduleById.ContainsKey(x.ScheduleId))
                .Where(x => scheduleById[x.ScheduleId].SemesterId == semesterId)
                .GroupBy(x => scheduleById[x.ScheduleId].SubjectId)
                .Select(grouping =>
                {
                    var total = grouping.Count();
                    var attended = grouping.Count(x => x.IsPresent);
                    var subjectId = grouping.Key;

                    return new SubjectAttendanceSummary
                    {
                        SubjectId = subjectId,
                        SubjectName = subjectById.TryGetValue(subjectId, out var subject)
                            ? subject.Name
                            : "Невідома дисципліна",
                        TotalClasses = total,
                        AttendedClasses = attended,
                        MissedClasses = total - attended
                    };
                })
                .ToList();

            academicSemesters.Add(new AcademicSemester
            {
                SemesterId = semester.Id,
                AcademicYear = semester.AcademicYear,
                SemesterNumber = semester.SemesterNumber,
                Grades = semesterGrades,
                Attendance = semesterAttendance
            });
        }

        return new StudentAcademicProfile
        {
            StudentId = student.UserId,
            StudentName = user.Name,
            Email = user.Email,
            Group = new StudyGroupInfo
            {
                Id = group.Id,
                Name = group.Name
            },
            Semesters = academicSemesters
                .OrderBy(x => x.AcademicYear)
                .ThenBy(x => x.SemesterNumber)
                .ToList()
        };
    }

    public async Task<bool> SyncStudentFromPostgresAsync(int studentId)
    {
        var profile = await GetProfileFromPostgresSqlAsync(studentId);

        if (profile == null)
        {
            await _profiles.DeleteOneAsync(x => x.StudentId == studentId);
            return false;
        }

        await _profiles.ReplaceOneAsync(
            x => x.StudentId == studentId,
            profile,
            new ReplaceOptions { IsUpsert = true });

        return true;
    }

    public async Task<int> SyncAllStudentsFromPostgresAsync()
    {
        var studentIds = await GetStudentIdsAsync();
        var synced = 0;

        foreach (var studentId in studentIds)
        {
            if (await SyncStudentFromPostgresAsync(studentId))
                synced++;
        }

        return synced;
    }

    public async Task<StudentAcademicProfile?> GetByStudentIdAsync(int studentId)
    {
        return await _profiles.Find(x => x.StudentId == studentId)
            .FirstOrDefaultAsync();
    }

    public Task<List<StudentAcademicProfile>> GetByGroupIdAsync(int groupId)
    {
        return _profiles.Find(x => x.Group.Id == groupId).ToListAsync();
    }

    public async Task<AcademicSemester?> GetSemesterAsync(int studentId, int semesterId)
    {
        var profile = await GetByStudentIdAsync(studentId);
        return profile?.Semesters.FirstOrDefault(x => x.SemesterId == semesterId);
    }

    public async Task<bool> UpdateGradeScoreAsync(int studentId, int gradeId, int newScore)
    {
        var profile = await GetByStudentIdAsync(studentId);
        if (profile == null)
            return false;

        var grade = profile.Semesters
            .SelectMany(x => x.Grades)
            .FirstOrDefault(x => x.GradeId == gradeId);

        if (grade == null)
            return false;

        grade.Score = newScore;
        await _profiles.ReplaceOneAsync(x => x.StudentId == studentId, profile);
        return true;
    }
    public async Task<UniversityDbConsole.Models.Grade?> GetGradeFromPostgresSqlAsync(
        int studentId,
        int gradeId)
    {
        return await _context.Grades
            .FromSqlInterpolated(
                $"SELECT * FROM v_grade WHERE studentid = {studentId} AND id = {gradeId}")
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }

    public async Task<UniversityDbConsole.Models.Subject?> GetUnusedSubjectForBenchmarkAsync(int subjectId)
    {
        return await _context.Subjects
            .FromSqlInterpolated(
                $"SELECT * FROM v_subject WHERE id = {subjectId} AND NOT isdeleted")
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }

    public async Task<int> GetNextGradeIdFromPostgresSqlAsync()
    {
        return await _context.Database
            .SqlQuery<int>(
                $"SELECT nextval(pg_get_serial_sequence('grades', 'id'))::int AS \"Value\"")
            .SingleAsync();
    }

    public async Task<int> UpdateGradeScoreInPostgresSqlAsync(
        int studentId,
        int gradeId,
        int newScore)
    {
        return await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE grades SET score = {newScore} WHERE id = {gradeId} AND studentid = {studentId}");
    }

    public async Task<bool> UpdateGradeScoreInMongoAsync(
        int studentId,
        int semesterId,
        int gradeId,
        int newScore)
    {
        var update = Builders<StudentAcademicProfile>.Update
            .Set("Semesters.$[semester].Grades.$[grade].Score", newScore);

        var options = new UpdateOptions
        {
            ArrayFilters = new List<ArrayFilterDefinition>
            {
                new BsonDocumentArrayFilterDefinition<BsonDocument>(
                    new BsonDocument("semester.SemesterId", semesterId)),
                new BsonDocumentArrayFilterDefinition<BsonDocument>(
                    new BsonDocument("grade.GradeId", gradeId))
            }
        };

        var result = await _profiles.UpdateOneAsync(
            x => x.StudentId == studentId,
            update,
            options);

        return result.ModifiedCount == 1;
    }

    public async Task<int?> GetGradeScoreFromPostgresSqlAsync(
        int studentId,
        int gradeId)
    {
        return await _context.Database
            .SqlQuery<int?>(
                $"SELECT score AS \"Value\" FROM grades WHERE id = {gradeId} AND studentid = {studentId}")
            .SingleOrDefaultAsync();
    }

    public async Task<AcademicGrade?> GetGradeFromMongoAsync(
        int studentId,
        int semesterId,
        int gradeId)
    {
        var profile = await GetByStudentIdAsync(studentId);

        return profile?.Semesters
            .FirstOrDefault(x => x.SemesterId == semesterId)?
            .Grades.FirstOrDefault(x => x.GradeId == gradeId);
    }

    public async Task<int> InsertGradeInPostgresSqlAsync(
        int gradeId,
        int studentId,
        int subjectId,
        int templateGradeId,
        int score)
    {
        return await _context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO grades (id, studentid, subjectid, teacherid, semesterid, examtypeid, score, lastmodifieddate, lastmodifiedby) SELECT {gradeId}, {studentId}, {subjectId}, teacherid, semesterid, examtypeid, {score}, CURRENT_TIMESTAMP, lastmodifiedby FROM grades WHERE id = {templateGradeId}");
    }

    public async Task<int> DeleteGradeFromPostgresSqlAsync(
        int studentId,
        int gradeId)
    {
        return await _context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM grades WHERE id = {gradeId} AND studentid = {studentId}");
    }

    public async Task<bool> AddGradeToMongoAsync(
        int studentId,
        int semesterId,
        AcademicGrade grade)
    {
        var filter = Builders<StudentAcademicProfile>.Filter.Eq(
            x => x.StudentId,
            studentId);

        filter &= Builders<StudentAcademicProfile>.Filter.ElemMatch(
            x => x.Semesters,
            x => x.SemesterId == semesterId);

        var update = Builders<StudentAcademicProfile>.Update.Push(
            "Semesters.$.Grades",
            grade);

        var result = await _profiles.UpdateOneAsync(filter, update);

        return result.ModifiedCount == 1;
    }

    public async Task<bool> RemoveGradeFromMongoAsync(
        int studentId,
        int semesterId,
        int gradeId)
    {
        var profile = await GetByStudentIdAsync(studentId);

        if (profile == null)
            return false;

        var semester = profile.Semesters
            .FirstOrDefault(x => x.SemesterId == semesterId);

        if (semester == null)
            return false;

        var removed = semester.Grades.RemoveAll(
            x => x.GradeId == gradeId);

        if (removed == 0)
            return false;

        await _profiles.ReplaceOneAsync(
            x => x.StudentId == studentId,
            profile);

        return true;
    }
}