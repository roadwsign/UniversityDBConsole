using System.Diagnostics;
using System.Text.Json;
using UniversityDbConsole.NoSql.Models;

namespace UniversityDbConsole.NoSql.Services;

public class DatabaseBenchmarkService
{
    private readonly StudentAcademicProfileService _profileService;

    public DatabaseBenchmarkService(StudentAcademicProfileService profileService)
    {
        _profileService = profileService;
    }

    public async Task RunAsync(int studentId)
    {
        const int warmupCount = 5;
        const int iterations = 100;

        var postgresProfile =
            await _profileService.GetProfileFromPostgresSqlAsync(studentId);

        var mongoProfile =
            await _profileService.GetByStudentIdAsync(studentId);

        if (postgresProfile == null)
        {
            Console.WriteLine("Академічну картку не знайдено в PostgreSQL.\n");
            return;
        }

        if (mongoProfile == null)
        {
            Console.WriteLine("Академічну картку не знайдено в MongoDB. Спочатку виконайте синхронізацію.\n");
            return;
        }

        var postgresJson = JsonSerializer.Serialize(postgresProfile);
        var mongoJson = JsonSerializer.Serialize(mongoProfile);

        if (postgresJson != mongoJson)
        {
            Console.WriteLine("Дані в PostgreSQL та MongoDB відрізняються.");
            Console.WriteLine("Рекомендується спочатку виконати синхронізацію.\n");
            return;
        }

        Console.WriteLine("Перевірка даних: результати обох баз однакові.\n");
        Console.WriteLine($"Студент: {postgresProfile.StudentName} (ID: {studentId})\n");

        await RunReadBenchmarkAsync(studentId, warmupCount, iterations);

        var semester = postgresProfile.Semesters
            .FirstOrDefault(x => x.Grades.Count > 0);

        var academicGrade = semester?.Grades.FirstOrDefault();

        if (semester == null || academicGrade == null)
        {
            Console.WriteLine("У студента немає оцінок. Тести оновлення та додавання пропущено.\n");
            return;
        }

        var templateGrade = await _profileService.GetGradeFromPostgresSqlAsync(
            studentId,
            academicGrade.GradeId);

        if (templateGrade == null)
        {
            Console.WriteLine("Не вдалося знайти оцінку для тестування запису.\n");
            return;
        }

        await RunUpdateBenchmarkAsync(
            studentId,
            semester.SemesterId,
            academicGrade,
            templateGrade,
            warmupCount,
            iterations);

        var unusedSubject = await _profileService.GetUnusedSubjectForBenchmarkAsync(2);

        if (unusedSubject == null)
        {
            Console.WriteLine("Не знайдено дисципліни без оцінки для тесту додавання.\n");
            return;
        }

        await RunInsertBenchmarkAsync(
            studentId,
            semester.SemesterId,
            academicGrade,
            templateGrade,
            unusedSubject,
            warmupCount,
            iterations);
    }

    private async Task RunReadBenchmarkAsync(
        int studentId,
        int warmupCount,
        int iterations)
    {
        Console.WriteLine("тест 1: читання");
        Console.WriteLine($"Прогрів: {warmupCount} повторів.");
        Console.WriteLine($"Вимірювання: {iterations} повторів.\n");

        for (int i = 0; i < warmupCount; i++)
        {
            await _profileService.GetProfileFromPostgresSqlAsync(studentId);
            await _profileService.GetByStudentIdAsync(studentId);
        }

        var postgresTimes = new List<double>();
        var mongoTimes = new List<double>();

        for (int i = 0; i < iterations; i++)
        {
            if (i % 2 == 0)
            {
                postgresTimes.Add(await MeasureReadAsync(
                    () => _profileService.GetProfileFromPostgresSqlAsync(studentId)));

                mongoTimes.Add(await MeasureReadAsync(
                    () => _profileService.GetByStudentIdAsync(studentId)));
            }
            else
            {
                mongoTimes.Add(await MeasureReadAsync(
                    () => _profileService.GetByStudentIdAsync(studentId)));

                postgresTimes.Add(await MeasureReadAsync(
                    () => _profileService.GetProfileFromPostgresSqlAsync(studentId)));
            }
        }

        PrintResults("PostgreSQL (SQL SELECT)", postgresTimes);
        PrintResults("MongoDB (Find)", mongoTimes);
        PrintComparison(postgresTimes, mongoTimes);
    }

    private async Task RunUpdateBenchmarkAsync(
        int studentId,
        int semesterId,
        AcademicGrade academicGrade,
        UniversityDbConsole.Models.Grade templateGrade,
        int warmupCount,
        int iterations)
    {
        Console.WriteLine("тест 2: оновлення оцінки");
        Console.WriteLine($"Оцінка ID: {academicGrade.GradeId}");
        Console.WriteLine($"Початковий бал: {templateGrade.Score}");

        var originalScore = templateGrade.Score;
        var newScore = originalScore == 100
            ? originalScore - 1
            : originalScore + 1;

        var postgresTimes = new List<double>();
        var mongoTimes = new List<double>();

        try
        {
            for (int i = 0; i < warmupCount; i++)
            {
                var score = i % 2 == 0 ? newScore : originalScore;

                await SetBothGradeScoresAsync(
                    studentId,
                    semesterId,
                    academicGrade.GradeId,
                    score);
            }

            await SetBothGradeScoresAsync(
                studentId,
                semesterId,
                academicGrade.GradeId,
                originalScore);

            for (int i = 0; i < iterations; i++)
            {
                var score = i % 2 == 0 ? newScore : originalScore;

                if (i % 2 == 0)
                {
                    postgresTimes.Add(await MeasureWriteAsync(
                        async () =>
                            await _profileService.UpdateGradeScoreInPostgresSqlAsync(
                                studentId,
                                academicGrade.GradeId,
                                score) == 1));

                    mongoTimes.Add(await MeasureWriteAsync(
                        () => _profileService.UpdateGradeScoreInMongoAsync(
                            studentId,
                            semesterId,
                            academicGrade.GradeId,
                            score)));
                }
                else
                {
                    mongoTimes.Add(await MeasureWriteAsync(
                        () => _profileService.UpdateGradeScoreInMongoAsync(
                            studentId,
                            semesterId,
                            academicGrade.GradeId,
                            score)));

                    postgresTimes.Add(await MeasureWriteAsync(
                        async () =>
                            await _profileService.UpdateGradeScoreInPostgresSqlAsync(
                                studentId,
                                academicGrade.GradeId,
                                score) == 1));
                }
            }

            var postgresScore =
                await _profileService.GetGradeScoreFromPostgresSqlAsync(
                    studentId,
                    academicGrade.GradeId);

            var mongoGrade =
                await _profileService.GetGradeFromMongoAsync(
                    studentId,
                    semesterId,
                    academicGrade.GradeId);

            if (postgresScore != originalScore ||
                mongoGrade?.Score != originalScore)
            {
                throw new InvalidOperationException(
                    "Після тесту оновлення оцінки дані в базах відрізняються.");
            }

            Console.WriteLine($"Прогрів: {warmupCount} повторів.");
            Console.WriteLine($"Вимірювання: {iterations} повторів.\n");

            PrintResults("PostgreSQL (SQL UPDATE)", postgresTimes);
            PrintResults("MongoDB (UpdateOne)", mongoTimes);
            PrintComparison(postgresTimes, mongoTimes);
        }
        finally
        {
            await _profileService.UpdateGradeScoreInPostgresSqlAsync(
                studentId,
                academicGrade.GradeId,
                originalScore);

            await _profileService.UpdateGradeScoreInMongoAsync(
                studentId,
                semesterId,
                academicGrade.GradeId,
                originalScore);
        }
    }

    private async Task SetBothGradeScoresAsync(
        int studentId,
        int semesterId,
        int gradeId,
        int score)
    {
        var postgresUpdated =
            await _profileService.UpdateGradeScoreInPostgresSqlAsync(
                studentId,
                gradeId,
                score);

        var mongoUpdated =
            await _profileService.UpdateGradeScoreInMongoAsync(
                studentId,
                semesterId,
                gradeId,
                score);

        if (postgresUpdated != 1 || !mongoUpdated)
        {
            throw new InvalidOperationException(
                "Не вдалося оновити оцінку в обох базах даних.");
        }
    }

    private async Task RunInsertBenchmarkAsync(
        int studentId,
        int semesterId,
        AcademicGrade templateAcademicGrade,
        UniversityDbConsole.Models.Grade templateGrade,
        UniversityDbConsole.Models.Subject subject,
        int warmupCount,
        int iterations)
    {
        Console.WriteLine("тест 3: додавання оцінки");
        Console.WriteLine($"Дисципліна: {subject.Name}");
        Console.WriteLine($"Семестр ID: {semesterId}");

        var gradeIds = new List<int>();

        for (int i = 0; i < warmupCount + iterations; i++)
        {
            gradeIds.Add(
                await _profileService.GetNextGradeIdFromPostgresSqlAsync());
        }

        var postgresTimes = new List<double>();
        var mongoTimes = new List<double>();

        for (int i = 0; i < warmupCount + iterations; i++)
        {
            var measure = i >= warmupCount;
            var postgresFirst = i % 2 == 0;

            await RunInsertIterationAsync(
                gradeIds[i],
                studentId,
                semesterId,
                templateAcademicGrade,
                templateGrade,
                subject,
                measure,
                postgresFirst,
                postgresTimes,
                mongoTimes);
        }

        Console.WriteLine($"Прогрів: {warmupCount} повторів.");
        Console.WriteLine($"Вимірювання: {iterations} повторів.\n");

        PrintResults("PostgreSQL (SQL INSERT)", postgresTimes);
        PrintResults("MongoDB (UpdateOne + Push)", mongoTimes);
        PrintComparison(postgresTimes, mongoTimes);
    }

    private async Task RunInsertIterationAsync(
        int gradeId,
        int studentId,
        int semesterId,
        AcademicGrade templateAcademicGrade,
        UniversityDbConsole.Models.Grade templateGrade,
        UniversityDbConsole.Models.Subject subject,
        bool measure,
        bool postgresFirst,
        List<double> postgresTimes,
        List<double> mongoTimes)
    {
        var newGrade = new AcademicGrade
        {
            GradeId = gradeId,
            SubjectId = subject.Id,
            SubjectName = subject.Name,
            TeacherId = templateAcademicGrade.TeacherId,
            TeacherName = templateAcademicGrade.TeacherName,
            ExamType = templateAcademicGrade.ExamType,
            Score = templateGrade.Score
        };

        Func<Task<bool>> postgresInsert = async () =>
            await _profileService.InsertGradeInPostgresSqlAsync(
                gradeId,
                studentId,
                subject.Id,
                templateGrade.Id,
                templateGrade.Score) == 1;

        Func<Task<bool>> mongoInsert = () =>
            _profileService.AddGradeToMongoAsync(
                studentId,
                semesterId,
                newGrade);

        try
        {
            if (postgresFirst)
            {
                if (measure)
                {
                    postgresTimes.Add(
                        await MeasureWriteAsync(postgresInsert));

                    mongoTimes.Add(
                        await MeasureWriteAsync(mongoInsert));
                }
                else
                {
                    if (!await postgresInsert())
                        throw new InvalidOperationException(
                            "Не вдалося додати оцінку в PostgreSQL.");

                    if (!await mongoInsert())
                        throw new InvalidOperationException(
                            "Не вдалося додати оцінку в MongoDB.");
                }
            }
            else
            {
                if (measure)
                {
                    mongoTimes.Add(
                        await MeasureWriteAsync(mongoInsert));

                    postgresTimes.Add(
                        await MeasureWriteAsync(postgresInsert));
                }
                else
                {
                    if (!await mongoInsert())
                        throw new InvalidOperationException(
                            "Не вдалося додати оцінку в MongoDB.");

                    if (!await postgresInsert())
                        throw new InvalidOperationException(
                            "Не вдалося додати оцінку в PostgreSQL.");
                }
            }

            var postgresGrade =
                await _profileService.GetGradeFromPostgresSqlAsync(
                    studentId,
                    gradeId);

            var mongoGrade =
                await _profileService.GetGradeFromMongoAsync(
                    studentId,
                    semesterId,
                    gradeId);

            if (postgresGrade == null ||
                mongoGrade == null ||
                postgresGrade.SubjectId != mongoGrade.SubjectId ||
                postgresGrade.TeacherId != mongoGrade.TeacherId ||
                postgresGrade.SemesterId != semesterId ||
                postgresGrade.Score != mongoGrade.Score ||
                mongoGrade.SubjectName != subject.Name ||
                mongoGrade.ExamType != templateAcademicGrade.ExamType)
            {
                throw new InvalidOperationException(
                    "Результати додавання оцінки в PostgreSQL та MongoDB відрізняються.");
            }
        }
        finally
        {
            await _profileService.DeleteGradeFromPostgresSqlAsync(
                studentId,
                gradeId);

            await _profileService.RemoveGradeFromMongoAsync(
                studentId,
                semesterId,
                gradeId);
        }
    }

    private async Task<double> MeasureReadAsync(
        Func<Task<StudentAcademicProfile?>> operation)
    {
        var stopwatch = Stopwatch.StartNew();

        var result = await operation();

        stopwatch.Stop();

        if (result == null)
            throw new InvalidOperationException(
                "Не вдалося отримати академічну картку.");

        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private async Task<double> MeasureWriteAsync(
        Func<Task<bool>> operation)
    {
        var stopwatch = Stopwatch.StartNew();

        var success = await operation();

        stopwatch.Stop();

        if (!success)
            throw new InvalidOperationException(
                "Операція запису не була виконана.");

        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static void PrintResults(
        string databaseName,
        List<double> times)
    {
        var sortedTimes = times.OrderBy(x => x).ToList();

        double average = times.Average();

        double median = sortedTimes.Count % 2 == 0
            ? (sortedTimes[sortedTimes.Count / 2 - 1] +
               sortedTimes[sortedTimes.Count / 2]) / 2
            : sortedTimes[sortedTimes.Count / 2];

        Console.WriteLine($"--{databaseName}");
        Console.WriteLine($"Середній час: {average:F3} мс");
        Console.WriteLine($"Медіанний час: {median:F3} мс");
        Console.WriteLine($"Мінімальний час: {sortedTimes.First():F3} мс");
        Console.WriteLine($"Максимальний час: {sortedTimes.Last():F3} мс");
    }

    private static void PrintComparison(
        List<double> postgresTimes,
        List<double> mongoTimes)
    {
        var postgresAverage = postgresTimes.Average();
        var mongoAverage = mongoTimes.Average();

        Console.WriteLine();

        if (postgresAverage > mongoAverage)
        {
            Console.WriteLine(
                $"MongoDB швидша в середньому у {postgresAverage / mongoAverage:F2} раза.");
        }
        else if (mongoAverage > postgresAverage)
        {
            Console.WriteLine(
                $"PostgreSQL швидша в середньому у {mongoAverage / postgresAverage:F2} раза.");
        }
        else
        {
            Console.WriteLine("Середній час виконання однаковий.");
        }

        Console.WriteLine();
    }
}