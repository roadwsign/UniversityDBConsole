using Microsoft.EntityFrameworkCore;
using UniversityDbConsole.Data;

namespace UniversityDbConsole.NoSql.Data;

public sealed class NoSqlDbContext : AppDbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var connectionString = Environment.GetEnvironmentVariable("UNIVERSITY_POSTGRES_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=universitydb;Username=placeholder;Password=placeholder";

        optionsBuilder
            .UseNpgsql(connectionString)
            .UseLowerCaseNamingConvention();
    }
}
