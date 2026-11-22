using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Data;
using TshwaneSPCA.Web.PublicServices;

namespace TshwaneSPCA.Web.Pages;

[EnableRateLimiting("public-form")]
[RequestSizeLimit(16_384)]
[RequestFormLimits(ValueLengthLimit = 4096, ValueCountLimit = 16)]
public sealed class EnquiriesModel(PublicData database, MailSettings mail, OutboxPayload outbox) : PageModel
{
    [BindProperty]
    public EnquiryInput Form { get; set; } = new();

    [TempData]
    public bool Received { get; set; }

    public bool Enabled => mail.Enabled;
    public string Title { get; private set; } = "";
    public string Category { get; private set; } = "";

    public IActionResult OnGet(string? category)
    {
        Form.RequestId = Guid.NewGuid();
        return SelectCategory(category) is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostAsync(string? category, CancellationToken cancellationToken)
    {
        var recipient = SelectCategory(category);
        if (recipient is null)
            return NotFound();
        if (!Enabled)
            return StatusCode(503);
        if (!string.IsNullOrEmpty(Form.Website))
            return Acknowledge();

        if (!Form.Consent)
            ModelState.AddModelError("Form.Consent", "Please confirm that we may use your details to reply.");
        if (!MailSettings.IsEmail(Form.Email?.Trim()))
            ModelState.AddModelError("Form.Email", "Enter a valid email address.");
        if (Form.RequestId == Guid.Empty)
            ModelState.AddModelError(string.Empty, "Reload the form before sending your enquiry.");
        if (!ModelState.IsValid)
            return Page();

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var enquiry = new Enquiry
        {
            Id = Form.RequestId,
            Category = Category,
            Name = Form.Name.Trim(),
            Email = Form.Email!.Trim(),
            Phone = Form.Phone?.Trim(),
            Message = Form.Message.Trim(),
            ConsentVersion = "enquiry-v1",
            ReceivedAt = now
        };

        database.Enquiries.Add(enquiry);
        database.Outbox.Add(outbox.Create(new EmailMessage(recipient, $"Tshwane SPCA: {Title}",
            $"Name: {enquiry.Name}\nEmail: {enquiry.Email}\nPhone: {enquiry.Phone}\n\n{enquiry.Message}",
            enquiry.Email), now));

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 1555 })
        {
            // The same form submission was already saved and queued.
        }

        return Acknowledge();
    }

    private IActionResult Acknowledge()
    {
        Received = true;
        return RedirectToPage("./Enquiries", new { category = Category });
    }

    private string? SelectCategory(string? category)
    {
        Category = category ?? "general";
        var (title, recipient) = Category switch
        {
            "general" => ("General enquiry", mail.GeneralTo),
            "volunteer" => ("Volunteering enquiry", mail.VolunteerTo),
            "donations" => ("Donation or sponsorship enquiry", mail.DonationsTo),
            _ => ("", null)
        };
        Title = title;
        return recipient;
    }

    public sealed class EnquiryInput
    {
        public Guid RequestId { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = "";
        [Required, EmailAddress, StringLength(254)]
        public string Email { get; set; } = "";
        [StringLength(40), Phone]
        public string? Phone { get; set; }
        [Required, StringLength(3000)]
        public string Message { get; set; } = "";
        public bool Consent { get; set; }
        public string? Website { get; set; }
    }
}
