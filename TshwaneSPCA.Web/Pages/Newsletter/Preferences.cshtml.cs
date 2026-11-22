using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Data;
using TshwaneSPCA.Web.PublicServices;

namespace TshwaneSPCA.Web.Pages.Newsletter;

[EnableRateLimiting("public-form")]
[RequestSizeLimit(16_384)]
[RequestFormLimits(ValueLengthLimit = 4096, ValueCountLimit = 16)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PreferencesModel(PublicData database, MailSettings mail, OutboxPayload outbox) : PageModel
{
    [BindProperty]
    public string? Operation { get; set; }

    [BindProperty]
    public string? Token { get; set; }

    public string? Result { get; private set; }

    public IActionResult OnGet(string? action, string? token)
    {
        Operation = action;
        Token = token;
        return ValidRequest ? Page() : BadRequest();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ValidRequest)
            return BadRequest();

        var hash = NewsletterToken.Hash(Token!);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var subscriber = Operation == "confirm"
            ? await database.Subscribers.SingleOrDefaultAsync(row =>
                row.ConfirmationHash == hash && row.ConfirmationExpiresAt > now, cancellationToken)
            : await database.Subscribers.SingleOrDefaultAsync(row => row.UnsubscribeHash == hash, cancellationToken);

        Result = "This link is no longer valid. You can request a new signup link.";
        if (subscriber is null)
            return Page();

        if (Operation == "confirm")
        {
            if (!mail.Enabled)
                return StatusCode(503);

            subscriber.ConfirmedAt = now;
            subscriber.UnsubscribedAt = null;
            subscriber.ConfirmationHash = null;
            subscriber.ConfirmationExpiresAt = null;
            var unsubscribe = NewsletterToken.Create();
            subscriber.UnsubscribeHash = NewsletterToken.Hash(unsubscribe);
            database.Outbox.Add(outbox.Create(new EmailMessage(subscriber.Email, "Your Tshwane SPCA subscription is confirmed",
                $"Thank you for signing up. You can unsubscribe here:\n{NewsletterToken.Link(mail, "unsubscribe", unsubscribe)}"), now));
        }
        else
        {
            subscriber.UnsubscribedAt = now;
            subscriber.UnsubscribeHash = null;
            subscriber.ConfirmationHash = null;
            subscriber.ConfirmationExpiresAt = null;
        }

        subscriber.Version = Guid.NewGuid();
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            Result = Operation == "confirm" ? "Your newsletter subscription is confirmed." : "You have unsubscribed.";
        }
        catch (DbUpdateConcurrencyException)
        {
            // The request was already handled by another submission.
        }
        return Page();
    }

    private bool ValidRequest => (Operation is "confirm" or "unsubscribe") && NewsletterToken.IsValid(Token);
}
