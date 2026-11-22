using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TshwaneSPCA.Data;

await using var database = new MigrationContextFactory().CreateDbContext(args);
await database.Database.MigrateAsync();
Console.WriteLine("Tshwane SPCA SQLite migrations are up to date.");

public sealed class MigrationContextFactory : IDesignTimeDbContextFactory<PublicData>
{
    public PublicData CreateDbContext(string[] args)
    {
        var directory = PublicData.ResolveDirectory(Directory.GetCurrentDirectory(),
            Environment.GetEnvironmentVariable("TSHWANE_DATA_DIR"));
        var options = new DbContextOptionsBuilder<PublicData>();
        PublicData.Configure(options, directory);
        return new PublicData(options.Options);
    }
}
