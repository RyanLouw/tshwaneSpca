using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TshwaneSPCA.Data;
using TshwaneSPCA.Web.PublicServices;

namespace TshwaneSPCA.Tests;

public sealed class PublicServicesFactory : WebApplicationFactory<Program>
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"tshwane-tests-{Guid.NewGuid():N}");

    public RecordingEmailSender Mail { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("TSHWANE_DATA_DIR", directory);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Mail);
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PublicData>().Database.MigrateAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    public sealed class RecordingEmailSender : IEmailSender
    {
        private readonly ConcurrentQueue<EmailMessage> messages = new();

        public Task SendAsync(Guid messageId, EmailMessage message, CancellationToken cancellationToken)
        {
            messages.Enqueue(message);
            return Task.CompletedTask;
        }

        public async Task<EmailMessage> WaitForAsync(Func<EmailMessage, bool> predicate)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            while (messages.FirstOrDefault(predicate) is not { } message)
                await Task.Delay(50, timeout.Token);
            return messages.First(predicate);
        }
    }
}
