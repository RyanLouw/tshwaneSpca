using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TshwaneSPCA.Data;
using TshwaneSPCA.Web.PublicServices;

namespace TshwaneSPCA.Tests;

public sealed class Phase1bTests(PublicServicesFactory factory) : IClassFixture<PublicServicesFactory>
{
    [Fact]
    public async Task EnquiryRequiresAntiforgeryAndConsentAndQueuesOnlyOnce()
    {
        await factory.InitializeDatabaseAsync();
        using var client = CreateClient();
        const string route = "/enquiries/volunteer";
        var form = await ReadFormAsync(client, route);
        form["Form.Name"] = "Volunteer Test";
        form["Form.Email"] = "volunteer-test@example.test";
        form["Form.Message"] = "I would like to volunteer this weekend.";
        form["Form.Consent"] = "false";

        using var badToken = await PostAsync(client, route, form.Where(field => field.Key != "__RequestVerificationToken"));
        Assert.Equal(HttpStatusCode.BadRequest, badToken.StatusCode);
        using var badConsent = await PostAsync(client, route, form);
        Assert.Contains("Please confirm that we may use your details", await badConsent.Content.ReadAsStringAsync());

        form["Form.Consent"] = "true";
        using var accepted = await PostAsync(client, route, form);
        using var repeated = await PostAsync(client, route, form);
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, repeated.StatusCode);

        var message = await factory.Mail.WaitForAsync(mail => mail.ReplyTo == form["Form.Email"]);
        Assert.Equal("volunteer@example.test", message.To);
        Assert.Contains(form["Form.Message"], message.Text);

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PublicData>();
        var enquiry = Assert.Single(await database.Enquiries.Where(row => row.Email == form["Form.Email"]).ToListAsync());
        Assert.Equal("volunteer", enquiry.Category);
        Assert.Equal("enquiry-v1", enquiry.ConsentVersion);
    }

    [Fact]
    public async Task NewsletterRequiresExplicitConfirmationAndSupportsUnsubscribe()
    {
        await factory.InitializeDatabaseAsync();
        using var client = CreateClient();
        const string email = "newsletter-test@example.test";
        var form = await ReadFormAsync(client, "/newsletter/signup");
        form["Form.Email"] = email;
        form["Form.Consent"] = "true";
        using var signup = await PostAsync(client, "/newsletter/signup", form);
        Assert.Equal(HttpStatusCode.Redirect, signup.StatusCode);

        var confirmation = await factory.Mail.WaitForAsync(mail => mail.To == email && mail.Subject.StartsWith("Confirm your"));
        var confirmUrl = ExtractLink(confirmation.Text);
        var confirmForm = await ReadFormAsync(client, confirmUrl);
        var pending = await ReadSubscriberAsync(email);
        Assert.Null(pending.ConfirmedAt);
        Assert.NotEqual(confirmForm["Token"], pending.ConfirmationHash);
        using var confirmed = await PostAsync(client, "/newsletter/preferences", confirmForm);
        Assert.Contains("Your newsletter subscription is confirmed.", await confirmed.Content.ReadAsStringAsync());
        var active = await ReadSubscriberAsync(email);
        Assert.NotNull(active.ConfirmedAt);
        Assert.Null(active.ConfirmationHash);

        var welcome = await factory.Mail.WaitForAsync(mail => mail.To == email && mail.Subject.Contains("is confirmed"));
        var unsubscribeForm = await ReadFormAsync(client, ExtractLink(welcome.Text));
        Assert.Null((await ReadSubscriberAsync(email)).UnsubscribedAt);
        Assert.NotEqual(unsubscribeForm["Token"], active.UnsubscribeHash);
        using var unsubscribed = await PostAsync(client, "/newsletter/preferences", unsubscribeForm);
        Assert.Contains("You have unsubscribed.", await unsubscribed.Content.ReadAsStringAsync());
        var inactive = await ReadSubscriberAsync(email);
        Assert.NotNull(inactive.UnsubscribedAt);
        Assert.Null(inactive.UnsubscribeHash);
    }

    [Fact]
    public async Task EventsShowDraftsOnlyAfterPublicationAndSeparateTheArchive()
    {
        await factory.InitializeDatabaseAsync();
        using var client = CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PublicData>();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        database.Announcements.AddRange(
            new Announcement { Slug = "current", Title = "Upcoming outreach", Summary = "A public event", Body = "Outreach details", PublishedAt = now - 30 },
            new Announcement { Slug = "old", Title = "Past event", Summary = "In the archive", Body = "Earlier details", PublishedAt = now - 100, EndsAt = now - 30 },
            new Announcement { Slug = "draft", Title = "Draft event", Summary = "Internal", Body = "Internal" });
        await database.SaveChangesAsync();

        var current = await client.GetStringAsync("/events");
        var archive = await client.GetStringAsync("/events/archive");
        Assert.Contains("Upcoming outreach", current);
        Assert.DoesNotContain("Past event", current);
        Assert.Contains("Past event", archive);
        Assert.DoesNotContain("Draft event", current + archive);
        Assert.Contains("Outreach details", await client.GetStringAsync("/events/item/current"));
        using var draft = await client.GetAsync("/events/item/draft");
        Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);
    }

    [Fact]
    public async Task PublicLinksOpenEnquiriesAndRouteEachCategoryToItsConfiguredInbox()
    {
        await using var routingFactory = new PublicServicesFactory();
        await routingFactory.InitializeDatabaseAsync();
        using var client = routingFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var home = await new HtmlParser().ParseDocumentAsync(await client.GetStringAsync("/"));
        var complaint = Assert.Single(home.QuerySelectorAll("#cruelty a[href^='mailto:']"));
        Assert.Equal("Use my email app", complaint.TextContent);
        var mail = routingFactory.Services.GetRequiredService<MailSettings>();
        foreach (var (section, draft) in new[]
        {
            ("#cruelty", mail.ComplaintDraft), ("#volunteer-forms", mail.VolunteerApplicationDraft)
        })
        {
            var gmail = Assert.Single(home.QuerySelectorAll($"{section} a[href^='https://mail.google.com/']"));
            var outlook = Assert.Single(home.QuerySelectorAll($"{section} a[href^='https://outlook.live.com/']"));
            foreach (var (link, subjectParameter) in new[] { (gmail, "su"), (outlook, "subject") })
            {
                var query = QueryHelpers.ParseQuery(new Uri(link.GetAttribute("href")!).Query);
                Assert.Equal(draft.To, query["to"].ToString());
                Assert.Equal(draft.Subject, query[subjectParameter].ToString());
                Assert.Equal(draft.Cc ?? "", query.GetValueOrDefault("cc").ToString());
                Assert.Equal("_blank", link.GetAttribute("target"));
            }
        }
        Assert.DoesNotContain("@", home.QuerySelector("#cruelty")!.TextContent);
        Assert.Empty(home.QuerySelectorAll("#cruelty img"));

        var categories = new[] { "waltloo", "centurion", "clinic-waltloo", "clinic-centurion", "sponsorship" };
        foreach (var category in categories)
        {
            var route = $"/enquiries/{category}";
            Assert.NotNull(home.QuerySelector($"a[href='{route}']"));
            var form = await ReadFormAsync(client, route);
            var visitor = $"{category}-visitor@example.test";
            form["Form.Name"] = "Test visitor";
            form["Form.Email"] = visitor;
            form["Form.Message"] = "Testing the public contact route.";
            form["Form.Consent"] = "true";
            using var accepted = await PostAsync(client, route, form);
            Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
            Assert.Contains("Your enquiry was received", await client.GetStringAsync(accepted.Headers.Location));
        }

        foreach (var inbox in categories)
        {
            var message = await routingFactory.Mail.WaitForAsync(mail => mail.ReplyTo == $"{inbox}-visitor@example.test");
            Assert.Equal($"{inbox}@example.test", message.To);
        }
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<Subscriber> ReadSubscriberAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PublicData>().Subscribers
            .AsNoTracking().SingleAsync(row => row.Email == email);
    }

    private static string ExtractLink(string text) =>
        new Uri(Regex.Match(text, @"https?://\S+").Value).PathAndQuery;

    private static async Task<Dictionary<string, string>> ReadFormAsync(HttpClient client, string url)
    {
        var document = await new HtmlParser().ParseDocumentAsync(await client.GetStringAsync(url));
        return document.QuerySelectorAll("form input[type='hidden']")
            .ToDictionary(element => element.GetAttribute("name")!, element => element.GetAttribute("value")!);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url,
        IEnumerable<KeyValuePair<string, string>> fields)
    {
        using var content = new FormUrlEncodedContent(fields);
        return await client.PostAsync(url, content);
    }
}
