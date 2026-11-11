using System.Text.Json;
using System.Text.Encodings.Web;
using MongoDB.Driver;
using StackExchange.Redis;
using UniversityDbConsole.NoSql.Data;
using UniversityDbConsole.NoSql.Models;
using UniversityDbConsole.NoSql.Services;

namespace UniversityDbConsole.NoSql;

public static class NoSqlMenu
{
    public static async Task RunAsync()
    {
        var mongoConnection = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING")
            ?? "mongodb://localhost:27017";
        var mongoDatabaseName = Environment.GetEnvironmentVariable("MONGODB_DATABASE")
            ?? "university_nosql";
        var redisConnection = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
            ?? "localhost:6379";

        try
        {
            using var context = new NoSqlDbContext();

            var mongoClient = new MongoClient(mongoConnection);
            var mongoDatabase = mongoClient.GetDatabase(mongoDatabaseName);
            var profilesCollection = mongoDatabase
                .GetCollection<StudentAcademicProfile>("studentAcademicProfiles");

            using var redis = await ConnectionMultiplexer.ConnectAsync(redisConnection);
            var redisDatabase = redis.GetDatabase();
            await redisDatabase.PingAsync();

            var profileService = new StudentAcademicProfileService(context, profilesCollection);
            var scheduleCache = new ScheduleCacheService(context, redisDatabase);

            await profileService.EnsureIndexesAsync();

            Console.WriteLine("Підключення до PostgreSQL, MongoDB та Redis виконано.\n");
            await ShowMenuAsync(profileService, scheduleCache);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex.Message}");
        }
    }

    private static async Task ShowMenuAsync(
        StudentAcademicProfileService profileService,
        ScheduleCacheService scheduleCache)
    {
        var benchmarkService = new DatabaseBenchmarkService(profileService);
        while (true)
        {
            Console.WriteLine("1. Синхронізувати академічні картки з PostgreSQL у MongoDB");
            Console.WriteLine("2. Отримати академічну картку студента");
            Console.WriteLine("3. Знайти академічні картки за ID групи");
            Console.WriteLine("4. Отримати оцінки студента за семестр");
            Console.WriteLine("5. Змінити оцінку в MongoDB");
            Console.WriteLine("6. Отримати розклад групи (Redis cache)");
            Console.WriteLine("7. Перевірити стан кешу розкладу");
            Console.WriteLine("8. Очистити кеш розкладу");
            Console.WriteLine("9. Порівняти продуктивність PostgreSQL та MongoDB");
            Console.WriteLine("0. Вийти");
            Console.Write("Ваш вибір: ");

            var choice = Console.ReadLine();
            Console.WriteLine();

            try
            {
                switch (choice)
                {
                    case "1":
                    {
                        var synced = await profileService.SyncAllStudentsFromPostgresAsync();
                        Console.WriteLine($"Синхронізовано академічних карток: {synced}\n");
                        break;
                    }
                    case "2":
                    {
                        var studentId = ReadInt("ID студента: ");
                        var profile = await profileService.GetByStudentIdAsync(studentId);
                        PrintProfile(profile);
                        break;
                    }
                    case "3":
                    {
                        var groupId = ReadInt("ID навчальної групи: ");
                        var profiles = await profileService.GetByGroupIdAsync(groupId);
                        Console.WriteLine($"Знайдено карток: {profiles.Count}");
                        foreach (var profile in profiles)
                            Console.WriteLine($"ID: {profile.StudentId}; {profile.StudentName}; група: {profile.Group.Name}");
                        Console.WriteLine();
                        break;
                    }
                    case "4":
                    {
                        var studentId = ReadInt("ID студента: ");
                        var semesterId = ReadInt("ID семестру: ");
                        var semester = await profileService.GetSemesterAsync(studentId, semesterId);

                        if (semester == null)
                        {
                            Console.WriteLine("Дані за цим семестром не знайдено.\n");
                            break;
                        }

                        Console.WriteLine($"Семестр: {semester.AcademicYear}, №{semester.SemesterNumber}");
                        if (semester.Grades.Count == 0)
                            Console.WriteLine("Оцінок поки немає.");
                        else
                        {
                            foreach (var grade in semester.Grades)
                                Console.WriteLine($"{grade.SubjectName}: {grade.Score}; " +
                                                  $"викладач: {grade.TeacherName}; " +
                                                  $"контроль: {grade.ExamType}; ID оцінки: {grade.GradeId}");
                            Console.WriteLine($"Середній бал: {semester.Grades.Average(x => x.Score):F2}");
                        }
                        Console.WriteLine();
                        break;
                    }
                    case "5":
                    {
                        var studentId = ReadInt("ID студента: ");
                        var gradeId = ReadInt("ID оцінки: ");
                        var newScore = ReadInt("Нова оцінка (0-100): ");
                        if (newScore < 0 || newScore > 100)
                        {
                            Console.WriteLine("Оцінка має бути в межах від 0 до 100.\n");
                            break;
                        }

                        var updated = await profileService.UpdateGradeScoreAsync(studentId, gradeId, newScore);
                        Console.WriteLine(updated
                            ? "Оцінку оновлено в MongoDB.\n"
                            : "Студента або оцінку не знайдено.\n");
                        break;
                    }
                    case "6":
                    {
                        var groupId = ReadInt("ID навчальної групи: ");
                        var semesterId = ReadInt("ID семестру: ");
                        var result = await scheduleCache.GetGroupScheduleAsync(groupId, semesterId);
                        Console.WriteLine(result.FromCache
                            ? "Джерело: Redis (cache hit)."
                            : "Джерело: PostgreSQL (cache miss; результат збережено в Redis).");

                        if (result.Items.Count == 0)
                            Console.WriteLine("Для цієї групи та семестру занять не знайдено.");

                        foreach (var item in result.Items)
                        {
                            Console.WriteLine(
                                $"{item.LessonTime:dd.MM.yyyy HH:mm} | {item.SubjectName} | " +
                                $"{item.TeacherName} | {item.AuditoriumType} " +
                                $"{item.BuildingNumber}-{item.RoomNumber}");
                        }
                        Console.WriteLine();
                        break;
                    }
                    case "7":
                    {
                        var groupId = ReadInt("ID навчальної групи: ");
                        var semesterId = ReadInt("ID семестру: ");
                        var exists = await scheduleCache.ExistsAsync(groupId, semesterId);
                        var ttl = await scheduleCache.GetTimeToLiveAsync(groupId, semesterId);
                        Console.WriteLine(exists
                            ? $"Кеш існує. Залишилось: {ttl?.ToString() ?? "необмежено"}.\n"
                            : "Кеш для цього розкладу відсутній.\n");
                        break;
                    }
                    case "8":
                    {
                        var groupId = ReadInt("ID навчальної групи: ");
                        var semesterId = ReadInt("ID семестру: ");
                        var deleted = await scheduleCache.InvalidateAsync(groupId, semesterId);
                        Console.WriteLine(deleted
                            ? "Запис кешу видалено.\n"
                            : "Запису кешу не було.\n");
                        break;
                    }
                    case "9":
                    {
                        var studentId = ReadInt("ID студента для тестування: ");
                        await benchmarkService.RunAsync(studentId);
                        break;
                    }
                    case "0":
                        return;
                    default:
                        Console.WriteLine("Невідома команда.\n");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка виконання операції: {ex.Message}\n");
            }
        }
    }

    private static int ReadInt(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            if (int.TryParse(Console.ReadLine(), out var value))
                return value;

            Console.WriteLine("Введіть ціле число.");
        }
    }

    private static void PrintProfile(StudentAcademicProfile? profile)
    {
        if (profile == null)
        {
            Console.WriteLine("Академічну картку не знайдено. Спочатку виконайте синхронізацію.\n");
            return;
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        Console.WriteLine(JsonSerializer.Serialize(profile, options));
        Console.WriteLine();
    }
}
