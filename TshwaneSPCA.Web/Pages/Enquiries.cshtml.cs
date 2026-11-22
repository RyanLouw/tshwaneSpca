using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using TshwaneSPCA.Data;
using TshwaneSPCA.Web.PublicServices;

namespace TshwaneSPCA.Web.Pages;

[EnableRateLimiting("public-form")]
[RequestSizeLimit(16_384)]
[RequestFormLimits(ValueLengthLimit = 4096, ValueCountLimit = 16)]
public sealed class EnquiriesModel(MailSettings mail, EnquiryQueue queue) : PageModel
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

        await queue.SaveAsync(enquiry, recipient, $"Tshwane SPCA: {Title}", cancellationToken);
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
            "waltloo" => ("Waltloo branch enquiry", mail.WaltlooTo ?? mail.GeneralTo),
            "centurion" => ("Centurion branch enquiry", mail.CenturionTo ?? mail.GeneralTo),
            "clinic-waltloo" => ("Waltloo clinic enquiry", mail.WaltlooClinicTo ?? mail.WaltlooTo ?? mail.GeneralTo),
            "clinic-centurion" => ("Centurion clinic enquiry", mail.CenturionClinicTo ?? mail.CenturionTo ?? mail.GeneralTo),
            "sponsorship" => ("Sponsor a kennel enquiry", mail.SponsorshipTo ?? mail.DonationsTo),
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
