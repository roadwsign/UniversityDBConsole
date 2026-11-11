using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using UniversityDbConsole.NoSql.Data;
using UniversityDbConsole.NoSql.Models;

namespace UniversityDbConsole.NoSql.Services;

public class ScheduleCacheService
{
    private readonly NoSqlDbContext _context;
    private readonly IDatabase _redis;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);

    public ScheduleCacheService(NoSqlDbContext context, IDatabase redis)
    {
        _context = context;
        _redis = redis;
    }

    public static string BuildKey(int groupId, int semesterId)
        => $"schedule:group:{groupId}:semester:{semesterId}";

    public async Task<(List<ScheduleCacheItem> Items, bool FromCache)> GetGroupScheduleAsync(
        int groupId,
        int semesterId)
    {
        var key = BuildKey(groupId, semesterId);
        var cachedValue = await _redis.StringGetAsync(key);

        if (cachedValue.HasValue)
        {
            var cachedItems = JsonSerializer.Deserialize<List<ScheduleCacheItem>>(cachedValue.ToString())
                ?? new List<ScheduleCacheItem>();
            return (cachedItems, true);
        }

        var schedules = await _context.Schedules.AsNoTracking()
            .Where(x => x.StudyGroupId == groupId && x.SemesterId == semesterId)
            .OrderBy(x => x.LessonTime)
            .ToListAsync();

        if (schedules.Count == 0)
        {
            await _redis.StringSetAsync(key, "[]", _cacheLifetime);
            return (new List<ScheduleCacheItem>(), false);
        }

        var subjectIds = schedules.Select(x => x.SubjectId).Distinct().ToList();
        var teacherIds = schedules.Select(x => x.TeacherId).Distinct().ToList();
        var auditoriumIds = schedules.Select(x => x.AuditoriumId).Distinct().ToList();

        var subjects = await _context.Subjects.AsNoTracking()
            .Where(x => subjectIds.Contains(x.Id))
            .ToListAsync();

        var teacherUsers = await _context.UserAccounts.AsNoTracking()
            .Where(x => teacherIds.Contains(x.Id) && !x.IsDeleted)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync();

        var auditoriums = await _context.Auditoriums.AsNoTracking()
            .Where(x => auditoriumIds.Contains(x.Id))
            .ToListAsync();

        var auditoriumTypeIds = auditoriums.Select(x => x.AuditoriumTypeId).Distinct().ToList();
        var auditoriumTypes = auditoriumTypeIds.Count == 0
            ? new List<UniversityDbConsole.Models.AuditoriumType>()
            : await _context.AuditoriumTypes.AsNoTracking()
                .Where(x => auditoriumTypeIds.Contains(x.Id))
                .ToListAsync();

        var group = await _context.StudyGroups.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == groupId);
        var semester = await _context.Semesters.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == semesterId);

        var subjectById = subjects.ToDictionary(x => x.Id);
        var teacherNameById = teacherUsers.ToDictionary(x => x.Id, x => x.Name);
        var auditoriumById = auditoriums.ToDictionary(x => x.Id);
        var auditoriumTypeById = auditoriumTypes.ToDictionary(x => x.Id, x => x.Name);

        var items = schedules.Select(schedule =>
        {
            auditoriumById.TryGetValue(schedule.AuditoriumId, out var auditorium);
            var auditoriumTypeName = auditorium != null && auditoriumTypeById.TryGetValue(
                auditorium.AuditoriumTypeId, out var typeName)
                ? typeName : "Не вказано";

            return new ScheduleCacheItem
            {
                ScheduleId = schedule.Id,
                LessonTime = schedule.LessonTime,
                StudyGroupId = schedule.StudyGroupId,
                StudyGroupName = group?.Name ?? "Невідома група",
                SemesterId = schedule.SemesterId,
                AcademicYear = semester?.AcademicYear ?? string.Empty,
                SemesterNumber = semester?.SemesterNumber ?? 0,
                SubjectName = subjectById.TryGetValue(schedule.SubjectId, out var subject)
                    ? subject.Name : "Невідома дисципліна",
                TeacherName = teacherNameById.TryGetValue(schedule.TeacherId, out var teacherName)
                    ? teacherName : "Невідомий викладач",
                AuditoriumType = auditoriumTypeName,
                BuildingNumber = auditorium?.BuildingNumber ?? string.Empty,
                RoomNumber = auditorium?.RoomNumber ?? string.Empty
            };
        }).ToList();

        var json = JsonSerializer.Serialize(items);
        await _redis.StringSetAsync(key, json, _cacheLifetime);
        return (items, false);
    }

    public Task<bool> InvalidateAsync(int groupId, int semesterId)
        => _redis.KeyDeleteAsync(BuildKey(groupId, semesterId));

    public Task<bool> ExistsAsync(int groupId, int semesterId)
        => _redis.KeyExistsAsync(BuildKey(groupId, semesterId));

    public Task<TimeSpan?> GetTimeToLiveAsync(int groupId, int semesterId)
        => _redis.KeyTimeToLiveAsync(BuildKey(groupId, semesterId));
}
