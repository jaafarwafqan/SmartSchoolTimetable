using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartSchoolTimetable.Infrastructure;

public sealed class LocalDbContextFactory : IDesignTimeDbContextFactory<LocalDbContext>
{
    public LocalDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<LocalDbContext>();
        builder.UseSqlite("Data Source=design-time.db");
        return new LocalDbContext(builder.Options);
    }
}
