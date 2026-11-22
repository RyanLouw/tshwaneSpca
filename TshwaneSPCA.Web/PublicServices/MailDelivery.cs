using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Data;

namespace TshwaneSPCA.Web.PublicServices;

public sealed class MailSettings
{
    public string Mode { get; init; } = "Disabled";
    public string PublicUrl { get; init; } = "http://localhost:5080";
    public string From { get; init; } = "";
    public string GeneralTo { get; init; } = "";
    public string VolunteerTo { get; init; } = "";
    public string DonationsTo { get; init; } = "";
    public string SmtpHost { get; init; } = "localhost";
    public int SmtpPort { get; init; } = 1025;
    public string ResendApiKey { get; init; } = "";

    public bool Enabled => Mode != "Disabled";

    public bool IsValid()
    {
        if (!Enabled)
            return true;

        if (!Uri.TryCreate(PublicUrl, UriKind.Absolute, out var url) ||
            url.AbsolutePath != "/" || url.Query.Length > 0 || url.Fragment.Length > 0 ||
            url.UserInfo.Length > 0 ||
            !IsEmail(From) || !IsEmail(GeneralTo) || !IsEmail(VolunteerTo) || !IsEmail(DonationsTo))
            return false;

        return Mode switch
        {
            "Smtp" => url.Scheme is "http" or "https" && !string.IsNullOrWhiteSpace(SmtpHost) &&
                      SmtpPort is > 0 and <= 65535,
            "Resend" => url.Scheme == "https" && !string.IsNullOrWhiteSpace(ResendApiKey),
            _ => false
        };
    }

    public static bool IsEmail(string? value) =>
        MailAddress.TryCreate(value, out var address) &&
        string.Equals(value, address.Address, StringComparison.Ordinal);
}

public sealed record EmailMessage(string To, string Subject, string Text, string? ReplyTo = null);

public interface IEmailSender
{
    Task SendAsync(Guid messageId, EmailMessage message, CancellationToken cancellationToken);
}

public sealed class MailpitSender(MailSettings settings) : IEmailSender
{
    public async Task SendAsync(Guid messageId, EmailMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort);
        using var mail = new MailMessage(settings.From, message.To, message.Subject, message.Text);
        if (message.ReplyTo is not null)
            mail.ReplyToList.Add(message.ReplyTo);
        await client.SendMailAsync(mail, cancellationToken);
    }
}

public sealed class ResendSender(HttpClient client, MailSettings settings) : IEmailSender
{
    public async Task SendAsync(Guid messageId, EmailMessage message, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ResendApiKey);
        request.Headers.Add("Idempotency-Key", messageId.ToString("N"));
        request.Content = JsonContent.Create(new
        {
            from = settings.From,
            to = new[] { message.To },
            subject = message.Subject,
            text = message.Text,
            reply_to = message.ReplyTo
        }, options: new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class OutboxPayload(IDataProtectionProvider protection)
{
    private readonly IDataProtector protector = protection.CreateProtector("TshwaneSPCA.EmailOutbox.v1");

    public OutboxMessage Create(EmailMessage message, long now, long? expiresAt = null) => new()
    {
        Payload = protector.Protect(JsonSerializer.Serialize(message)),
        NextAttemptAt = now,
        ExpiresAt = expiresAt
    };

    public EmailMessage Read(string payload) =>
        JsonSerializer.Deserialize<EmailMessage>(protector.Unprotect(payload))!;
}

public sealed class OutboxWorker(IServiceScopeFactory scopes, ILogger<OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await DeliverNextAsync(stoppingToken))
                    continue;
            }
            catch (Exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("The email queue is temporarily unavailable.");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private async Task<bool> DeliverNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PublicData>();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var eligible = database.Outbox.Where(row => row.CompletedAt == null && row.FailedAt == null &&
            row.NextAttemptAt <= now && (row.LeaseUntil == null || row.LeaseUntil <= now));
        var item = await eligible.AsNoTracking().OrderBy(row => row.NextAttemptAt).ThenBy(row => row.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (item is null)
            return false;

        var lease = Guid.NewGuid();
        if (await eligible.Where(row => row.Id == item.Id).ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.LeaseId, lease)
                .SetProperty(row => row.LeaseUntil, now + 120), cancellationToken) == 0)
            return true;

        var claimed = database.Outbox.Where(row => row.Id == item.Id && row.LeaseId == lease);
        if (item.ExpiresAt <= now)
        {
            await claimed.ExecuteUpdateAsync(setters => setters.SetProperty(row => row.FailedAt, now)
                .SetProperty(row => row.Payload, ""), cancellationToken);
            return true;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var payload = scope.ServiceProvider.GetRequiredService<OutboxPayload>().Read(item.Payload);
            var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            await sender.SendAsync(item.Id, payload, timeout.Token);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            var attempts = item.Attempts + 1;
            var delay = Math.Min(3600L, 30L << Math.Min(attempts - 1, 7));
            var stop = attempts >= 10;
            await claimed.ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.Attempts, attempts)
                .SetProperty(row => row.NextAttemptAt, now + delay)
                .SetProperty(row => row.LeaseId, (Guid?)null)
                .SetProperty(row => row.LeaseUntil, (long?)null)
                .SetProperty(row => row.FailedAt, stop ? now : null), cancellationToken);
            logger.LogWarning("Email queue item {MessageId} failed on attempt {Attempt}.", item.Id, attempts);
            return true;
        }

        await claimed.ExecuteUpdateAsync(setters => setters
            .SetProperty(row => row.CompletedAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            .SetProperty(row => row.Payload, "")
            .SetProperty(row => row.LeaseId, (Guid?)null)
            .SetProperty(row => row.LeaseUntil, (long?)null), cancellationToken);
        return true;
    }
}
