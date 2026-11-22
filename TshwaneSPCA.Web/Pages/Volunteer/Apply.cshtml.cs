using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using TshwaneSPCA.Data;
using TshwaneSPCA.Web.PublicServices;

namespace TshwaneSPCA.Web.Pages.Volunteer;

[EnableRateLimiting("public-form")]
[RequestSizeLimit(MaxTotalBytes + 64 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = MaxTotalBytes, ValueLengthLimit = 4096, ValueCountLimit = 16)]
public sealed class ApplyModel(MailSettings mail, EnquiryQueue queue) : PageModel
{
    private const int MaxTotalBytes = 5 * 1024 * 1024;
    private const int MaxDocuments = 3;

    [BindProperty]
    public ApplicationInput Form { get; set; } = new();

    [TempData]
    public bool ApplicationReceived { get; set; }

    public bool Enabled => mail.Enabled;

    public void OnGet() => Form.RequestId = Guid.NewGuid();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!Enabled)
            return StatusCode(503);
        if (!string.IsNullOrEmpty(Form.Website))
            return Acknowledge();

        if (Form.RequestId == Guid.Empty)
            ModelState.AddModelError(string.Empty, "Reload the form before submitting your application.");
        if (!MailSettings.IsEmail(Form.Email?.Trim()))
            ModelState.AddModelError("Form.Email", "Enter a valid email address.");
        if (!Form.Consent)
            ModelState.AddModelError("Form.Consent", "Please agree to the use of your details for your application.");
        if (Form.Documents.Count is < 1 or > MaxDocuments || Form.Documents.Sum(file => file.Length) > MaxTotalBytes)
            ModelState.AddModelError("Form.Documents", "Attach one to three PDF files totalling no more than 5 MB.");
        if (!ModelState.IsValid)
            return Page();

        var attachments = new List<EmailAttachment>(Form.Documents.Count);
        foreach (var document in Form.Documents)
        {
            if (!string.Equals(Path.GetExtension(document.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)
                || document.Length < 12)
                return InvalidPdf();

            using var buffer = new MemoryStream((int)document.Length);
            await document.CopyToAsync(buffer, cancellationToken);
            var content = buffer.ToArray();
            if (!HasPdfMarkers(content))
                return InvalidPdf();

            // Use a fixed name instead of copying untrusted multipart metadata into the email.
            attachments.Add(new EmailAttachment($"volunteer-application-{attachments.Count + 1}.pdf", content));
        }

        var enquiry = new Enquiry
        {
            Id = Form.RequestId,
            Category = "volunteer-application",
            Name = Form.Name.Trim(),
            Email = Form.Email!.Trim(),
            Message = string.IsNullOrWhiteSpace(Form.Message) ? "Signed volunteer application attached." : Form.Message.Trim(),
            ConsentVersion = "volunteer-application-v1",
            ReceivedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await queue.SaveAsync(enquiry, mail.VolunteerApplicationsTo, "Tshwane SPCA: Completed Volunteer Application",
            cancellationToken, mail.VolunteerApplicationsCc, attachments);
        return Acknowledge();
    }

    private IActionResult InvalidPdf()
    {
        ModelState.AddModelError("Form.Documents", "Attach completed PDF files only.");
        return Page();
    }

    private IActionResult Acknowledge()
    {
        ApplicationReceived = true;
        return RedirectToPage();
    }

    private static bool HasPdfMarkers(ReadOnlySpan<byte> content)
    {
        if (!content.StartsWith("%PDF-"u8))
            return false;

        var end = content.Length;
        while (end > 0 && content[end - 1] is 0 or 9 or 10 or 12 or 13 or 32)
            end--;
        return content[..end].EndsWith("%%EOF"u8);
    }

    public sealed class ApplicationInput
    {
        public Guid RequestId { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = "";
        [Required, EmailAddress, StringLength(254)]
        public string Email { get; set; } = "";
        [StringLength(3000)]
        public string? Message { get; set; }
        public List<IFormFile> Documents { get; set; } = [];
        public bool Consent { get; set; }
        public string? Website { get; set; }
    }
}
