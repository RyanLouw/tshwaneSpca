using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Data;
using TshwaneSPCA.Web.PublicServices;

namespace TshwaneSPCA.Web.Pages.Newsletter;

[EnableRateLimiting("public-form")]
[RequestSizeLimit(16_384)]
[RequestFormLimits(ValueLengthLimit = 4096, ValueCountLimit = 16)]
public sealed class SignupModel(PublicData database, MailSettings mail, OutboxPayload outbox) : PageModel
{
    [BindProperty]
    public SignupInput Form { get; set; } = new();

    [TempData]
    public bool Requested { get; set; }

    public bool Enabled => mail.Enabled;

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!Enabled)
            return StatusCode(503);
        if (!string.IsNullOrEmpty(Form.Website))
            return Acknowledge();

        if (!Form.Consent)
            ModelState.AddModelError("Form.Consent", "Please confirm that you want to receive the newsletter.");
        var email = Form.Email?.Trim();
        if (!MailSettings.IsEmail(email))
            ModelState.AddModelError("Form.Email", "Enter a valid email address.");
        if (!ModelState.IsValid)
            return Page();

        var normalized = email!.ToUpperInvariant();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var subscriber = await database.Subscribers.SingleOrDefaultAsync(row => row.NormalizedEmail == normalized,
            cancellationToken);
        if (subscriber is null)
        {
            subscriber = new Subscriber
            {
                Email = email, NormalizedEmail = normalized, ConsentVersion = "newsletter-v1"
            };
            database.Subscribers.Add(subscriber);
        }
        else if ((subscriber.ConfirmedAt != null && subscriber.UnsubscribedAt == null) ||
                 subscriber.ConfirmationExpiresAt > now)
        {
            return Acknowledge();
        }

        var token = NewsletterToken.Create();
        subscriber.ConsentAt = now;
        subscriber.ConsentVersion = "newsletter-v1";
        subscriber.ConfirmationHash = NewsletterToken.Hash(token);
        subscriber.ConfirmationExpiresAt = now + 24 * 60 * 60;
        subscriber.Version = Guid.NewGuid();
        var link = NewsletterToken.Link(mail, "confirm", token);
        database.Outbox.Add(outbox.Create(new EmailMessage(subscriber.Email, "Confirm your Tshwane SPCA newsletter signup",
            $"Confirm your subscription within 24 hours:\n{link}\n\nIf you didn't request this, you can ignore this message."),
            now, subscriber.ConfirmationExpiresAt));

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A competing request already updated this subscriber.
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            // Another request already won the unique email insert.
        }

        return Acknowledge();
    }

    private IActionResult Acknowledge()
    {
        Requested = true;
        return RedirectToPage();
    }

    public sealed class SignupInput
    {
        [Required, EmailAddress, StringLength(254)]
        public string Email { get; set; } = "";
        public bool Consent { get; set; }
        public string? Website { get; set; }
    }
}
