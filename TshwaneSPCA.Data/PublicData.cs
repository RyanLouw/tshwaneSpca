using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TshwaneSPCA.Data;

public sealed class PublicData(DbContextOptions<PublicData> options) : DbContext(options)
{
    public DbSet<Enquiry> Enquiries => Set<Enquiry>();
    public DbSet<Subscriber> Subscribers => Set<Subscriber>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<Announcement> Announcements => Set<Announcement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subscriber>().HasIndex(row => row.NormalizedEmail).IsUnique();
        modelBuilder.Entity<Subscriber>().HasIndex(row => row.ConfirmationHash).IsUnique();
        modelBuilder.Entity<Subscriber>().HasIndex(row => row.UnsubscribeHash).IsUnique();
        modelBuilder.Entity<Subscriber>().Property(row => row.Version).IsConcurrencyToken();
        modelBuilder.Entity<OutboxMessage>().HasIndex(row => new { row.CompletedAt, row.NextAttemptAt });
        modelBuilder.Entity<Announcement>().HasIndex(row => row.Slug).IsUnique();
    }

    public static string ResolveDirectory(string contentRoot, string? configuredDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
            return Path.GetFullPath(configuredDirectory, contentRoot);

        for (var directory = new DirectoryInfo(contentRoot); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TshwaneSPCA.slnx")))
                return Path.Combine(directory.FullName, ".data");
        }

        return Path.Combine(contentRoot, ".data");
    }

    public static void Configure(DbContextOptionsBuilder options, string directory)
    {
        Directory.CreateDirectory(directory);
        options.UseSqlite(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, "tshwane-spca.db"),
            DefaultTimeout = 10
        }.ToString());
    }
}

public sealed class Enquiry
{
    public Guid Id { get; init; }
    public required string Category { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public string? Phone { get; init; }
    public required string Message { get; init; }
    public required string ConsentVersion { get; init; }
    public long ReceivedAt { get; init; }
}

public sealed class Subscriber
{
    public long Id { get; set; }
    public required string Email { get; init; }
    public required string NormalizedEmail { get; init; }
    public required string ConsentVersion { get; set; }
    public long ConsentAt { get; set; }
    public long? ConfirmedAt { get; set; }
    public long? UnsubscribedAt { get; set; }
    public string? ConfirmationHash { get; set; }
    public long? ConfirmationExpiresAt { get; set; }
    public string? UnsubscribeHash { get; set; }
    public Guid Version { get; set; }
}

public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Payload { get; set; }
    public long NextAttemptAt { get; set; }
    public long? ExpiresAt { get; init; }
    public long? CompletedAt { get; set; }
    public long? FailedAt { get; set; }
    public int Attempts { get; set; }
    public Guid? LeaseId { get; set; }
    public long? LeaseUntil { get; set; }
}

public sealed class Announcement
{
    public long Id { get; set; }
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string Body { get; init; }
    public string? Location { get; init; }
    public string? ImagePath { get; init; }
    public long? PublishedAt { get; init; }
    public long? StartsAt { get; init; }
    public long? EndsAt { get; init; }
    public long? ArchivedAt { get; init; }
}
