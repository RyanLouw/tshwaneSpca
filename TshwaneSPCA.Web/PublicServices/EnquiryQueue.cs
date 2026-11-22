using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Data;

namespace TshwaneSPCA.Web.PublicServices;

public sealed class EnquiryQueue(PublicData database, OutboxPayload outbox)
{
    public async Task SaveAsync(Enquiry enquiry, string recipient, string subject, CancellationToken cancellationToken,
        string? cc = null, IReadOnlyList<EmailAttachment>? attachments = null)
    {
        var message = new EmailMessage(recipient, subject,
            $"Name: {enquiry.Name}\nEmail: {enquiry.Email}\nPhone: {enquiry.Phone}\n\n{enquiry.Message}",
            enquiry.Email, cc, attachments);
        long? expiresAt = attachments is null ? null : enquiry.ReceivedAt + 24 * 60 * 60;
        database.Enquiries.Add(enquiry);
        database.Outbox.Add(outbox.Create(message, enquiry.ReceivedAt, expiresAt));

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 1555 })
        {
            // The same form submission was already saved and queued.
        }
    }
}
