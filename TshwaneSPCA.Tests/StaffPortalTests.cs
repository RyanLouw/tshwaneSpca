using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using TshwaneSPCA.Data;
using TshwaneSPCA.Web.Staff;

namespace TshwaneSPCA.Tests;

public sealed class StaffPortalTests
{
    [Fact]
    public async Task InviteOnlyStaffCanSetPasswordResetAndSignOut()
    {
        await using var factory = new PublicServicesFactory();
        await factory.InitializeDatabaseAsync();
        using var client = CreateClient(factory);
        await AssertRedirectAsync(client.GetAsync("/staff"), "/staff/login");
        await AssertRedirectAsync(client.GetAsync("/staff/subscribers?handler=Export"), "/staff/login");
        using var register = await client.GetAsync("/staff/register");
        Assert.Equal(HttpStatusCode.NotFound, register.StatusCode);

        const string first = "first-staff@example.test";
        await InviteFirstAsync(factory, first);
        var invitation = await factory.Mail.WaitForAsync(mail => mail.To == first);
        var inviteLink = ReadLink(invitation.Text);
        var firstPassword = "Start!Staff2026";
        await ChoosePasswordAsync(client, inviteLink, firstPassword);
        await SignInAsync(client, first, firstPassword);
        Assert.Contains("Staff portal", await client.GetStringAsync("/staff"));

        const string second = "second-staff@example.test";
        using var invite = await SubmitAsync(client, "/staff/accounts", new() { ["Email"] = second });
        Assert.Equal(HttpStatusCode.Redirect, invite.StatusCode);
        var secondInvite = await factory.Mail.WaitForAsync(mail => mail.To == second);
        using var secondClient = CreateClient(factory);
        await ChoosePasswordAsync(secondClient, ReadLink(secondInvite.Text), firstPassword);
        await SignInAsync(secondClient, second, firstPassword);
        using (var restricted = await secondClient.GetAsync("/staff/content"))
            Assert.Equal(HttpStatusCode.Forbidden, restricted.StatusCode);
        using (var accountsResponse = await secondClient.GetAsync("/staff/accounts"))
            Assert.Equal(HttpStatusCode.Forbidden, accountsResponse.StatusCode);

        await using var accountScope = factory.Services.CreateAsyncScope();
        var memberId = (await accountScope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>()
            .FindByEmailAsync(second))!.Id;
        var accessPage = await new HtmlParser().ParseDocumentAsync(await client.GetStringAsync("/staff/accounts"));
        var permissionForm = accessPage.QuerySelectorAll("form")
            .Single(form => form.GetAttribute("action")?.Contains("handler=Permissions") == true);
        var permissionToken = permissionForm.QuerySelector("input[name='__RequestVerificationToken']")!.GetAttribute("value")!;
        await AssertRedirectAsync(PostAsync(client, $"/staff/accounts?id={memberId}&handler=Permissions", new()
        {
            ["__RequestVerificationToken"] = permissionToken,
            ["modules"] = StaffAccess.Content
        }), "/staff/accounts");

        var content = await ReadFieldsAsync(secondClient, "/staff/content");
        Assert.DoesNotContain("Values[BankAccountNumber]", content.Keys);
        using (var export = await secondClient.GetAsync("/staff/subscribers?handler=Export"))
            Assert.Equal(HttpStatusCode.Forbidden, export.StatusCode);
        content["Values[HomeTitle]"] = "Staff can edit this heading";
        content["Values[BankAccountNumber]"] = "1234567890";
        using (var attempt = await PostAsync(secondClient, "/staff/content", content))
            Assert.Equal(HttpStatusCode.Forbidden, attempt.StatusCode);
        var unchanged = await ReadFieldsAsync(client, "/staff/content");
        Assert.Equal("561 6000 7452", unchanged["Values[BankAccountNumber]"]);
        Assert.NotEqual("Staff can edit this heading", unchanged["Values[HomeTitle]"]);
        content.Remove("Values[BankAccountNumber]");
        await AssertRedirectAsync(PostAsync(secondClient, "/staff/content", content), "/staff/content");
        Assert.Equal("Staff can edit this heading", (await ReadFieldsAsync(client, "/staff/content"))["Values[HomeTitle]"]);

        await SignOutAsync(client);
        await AssertRedirectAsync(client.GetAsync("/staff"), "/staff/login");
        using var reset = await SubmitAsync(client, "/staff/forgot-password", new() { ["Form.Email"] = first });
        Assert.Contains("If this is a staff account", (await reset.Content.ReadAsStringAsync()).Replace("&#x2019;", "'"));
        var resetMail = await factory.Mail.WaitForAsync(mail => mail.To == first && mail.Subject.StartsWith("Reset"));
        const string updatedPassword = "Revised!Staff2026";
        await ChoosePasswordAsync(client, ReadLink(resetMail.Text), updatedPassword);

        var old = await SubmitAsync(client, "/staff/login", new()
        {
            ["Form.Email"] = first, ["Form.Password"] = firstPassword
        });
        using (old)
            Assert.Contains("Sign-in failed", await old.Content.ReadAsStringAsync());
        await SignInAsync(client, first, updatedPassword);

        await using var scope = factory.Services.CreateAsyncScope();
        var member = await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(second);
        var accounts = await new HtmlParser().ParseDocumentAsync(await client.GetStringAsync("/staff/accounts"));
        var removal = accounts.QuerySelectorAll("form").Single(form => form.GetAttribute("action")?.Contains("handler=Remove") == true);
        var token = removal.QuerySelector("input[name='__RequestVerificationToken']")!.GetAttribute("value")!;
        await AssertRedirectAsync(PostAsync(client, $"/staff/accounts?id={member!.Id}&handler=Remove",
            new() { ["__RequestVerificationToken"] = token }), "/staff/accounts");
        await AssertRedirectAsync(secondClient.GetAsync("/staff"), "/staff/login");
    }

    [Fact]
    public async Task StaffCanPublishWebsiteCopyEventsAndMediaAndReadPrivateRecords()
    {
        await using var factory = new PublicServicesFactory();
        await factory.InitializeDatabaseAsync();
        using var client = CreateClient(factory);
        using var anonymous = CreateClient(factory);
        const string email = "publisher@example.test";
        const string password = "Preview!Staff2026";
        await InviteFirstAsync(factory, email);
        await ChoosePasswordAsync(client, ReadLink((await factory.Mail.WaitForAsync(mail => mail.To == email)).Text), password);
        await SignInAsync(client, email, password);

        var form = await ReadFieldsAsync(client, "/staff/content");
        form["Values[HomeTitle]"] = "A new home for every animal";
        form["Values[BankAccountNumber]"] = "1234567890";
        await AssertRedirectAsync(PostAsync(client, "/staff/content", form), "/Staff/Content");
        var homepage = await anonymous.GetStringAsync("/");
        Assert.Contains("A new home for every animal", homepage);
        Assert.Contains("data-copy=\"1234567890\"", homepage);
        Assert.DoesNotContain("/assets/donation-banking-details.jpg", homepage);
        Assert.DoesNotContain("/assets/sponsor-a-kennel-preview.jpg", homepage);

        var pdf = await anonymous.GetByteArrayAsync("/documents/community-hours-application.pdf");
        await UploadMediaAsync(client, "0", "New SPCA newsletter", "issue.pdf", pdf,
            new() { ["Form.EditionDate"] = "2026-11-22" });
        var newsletter = await ReadAssetAsync(factory, "New SPCA newsletter");
        using (var draft = await anonymous.GetAsync($"/library/file/{newsletter.Id}"))
            Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);
        using (var preview = await client.GetAsync($"/staff/media?handler=Preview&id={newsletter.Id}"))
            Assert.Equal(pdf, await preview.Content.ReadAsByteArrayAsync());

        var newsletterValues = new Dictionary<string, string>
        {
            ["Form.Title"] = "New SPCA newsletter", ["Form.EditionDate"] = "2026-11-22",
            ["Form.Published"] = "true", ["Form.Archived"] = "false"
        };
        await AssertRedirectAsync(SubmitAsync(client, $"/staff/media?id={newsletter.Id}", newsletterValues), "/Staff/Media");
        Assert.Contains("New SPCA newsletter", await anonymous.GetStringAsync("/"));
        using (var download = await anonymous.GetAsync($"/library/file/{newsletter.Id}"))
        {
            Assert.Equal("application/pdf", download.Content.Headers.ContentType!.MediaType);
            Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
            Assert.Equal(pdf, await download.Content.ReadAsByteArrayAsync());
        }

        newsletterValues["Form.Archived"] = "true";
        await AssertRedirectAsync(SubmitAsync(client, $"/staff/media?id={newsletter.Id}", newsletterValues), "/Staff/Media");
        Assert.DoesNotContain("New SPCA newsletter", await anonymous.GetStringAsync("/"));
        Assert.Contains("New SPCA newsletter", await anonymous.GetStringAsync("/library/newsletters"));

        var photo = await anonymous.GetByteArrayAsync("/assets/hero-tabby-cat.jpg");
        await UploadMediaAsync(client, "1", "Tabby gallery photo", "tabby.jpg", photo,
            new() { ["Form.AltText"] = "Fluffy tabby cat", ["Form.Published"] = "true" });
        var photoAsset = await ReadAssetAsync(factory, "Tabby gallery photo");
        Assert.Contains("Tabby gallery photo", await anonymous.GetStringAsync("/library/photos"));
        await UploadMediaAsync(client, "2", "Community hours guide", "guide.pdf", pdf,
            new() { ["Form.Published"] = "true" });
        Assert.Contains("Community hours guide", await anonymous.GetStringAsync("/library/documents"));

        var announcement = new Dictionary<string, string>
        {
            ["Form.Title"] = "New adoption event", ["Form.Summary"] = "An adoption event",
            ["Form.Body"] = "Meet the team.", ["Form.ImageId"] = photoAsset.Id.ToString(), ["Form.State"] = "0"
        };
        await AssertRedirectAsync(SubmitAsync(client, "/staff/events", announcement), "/Staff/Events");
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PublicData>();
        var entry = await database.Announcements.AsNoTracking().SingleAsync();
        using (var draft = await anonymous.GetAsync($"/events/item/{entry.Slug}"))
            Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);
        announcement["Form.State"] = "1";
        await AssertRedirectAsync(SubmitAsync(client, $"/staff/events?id={entry.Id}", announcement), "/Staff/Events");
        Assert.Contains("New adoption event", await anonymous.GetStringAsync("/events"));
        Assert.Contains($"/library/file/{photoAsset.Id}", await anonymous.GetStringAsync($"/events/item/{entry.Slug}"));
        announcement["Form.State"] = "2";
        await AssertRedirectAsync(SubmitAsync(client, $"/staff/events?id={entry.Id}", announcement), "/Staff/Events");
        Assert.Contains("New adoption event", await anonymous.GetStringAsync("/events/archive"));

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        database.Enquiries.Add(new Enquiry
        {
            Id = Guid.NewGuid(), Category = "volunteer", Name = "Volunteer Test", Email = "volunteer@example.test",
            Message = "Looking to help.", ConsentVersion = "test-v1", ReceivedAt = now
        });
        database.Subscribers.AddRange(
            new Subscriber { Email = "=active@example.test", NormalizedEmail = "=ACTIVE@EXAMPLE.TEST",
                ConsentVersion = "test-v1", ConsentAt = now, ConfirmedAt = now },
            new Subscriber { Email = "inactive@example.test", NormalizedEmail = "INACTIVE@EXAMPLE.TEST",
                ConsentVersion = "test-v1", ConsentAt = now, ConfirmedAt = now, UnsubscribedAt = now });
        await database.SaveChangesAsync();
        Assert.Contains("Volunteer Test", await client.GetStringAsync("/staff/enquiries?search=help&category=volunteer"));
        Assert.DoesNotContain("Volunteer Test", await client.GetStringAsync("/staff/enquiries?search=help&category=general"));
        using var export = await client.GetAsync("/staff/subscribers?handler=Export");
        Assert.True(export.Headers.CacheControl?.NoStore);
        var csv = await export.Content.ReadAsStringAsync();
        Assert.Contains("\"'=active@example.test\"", csv);
        Assert.DoesNotContain("inactive@example.test", csv);
        await AssertRedirectAsync(anonymous.GetAsync("/staff/enquiries"), "/staff/login");
        await AssertRedirectAsync(anonymous.GetAsync("/staff/subscribers?handler=Export"), "/staff/login");
    }

    [Fact]
    public async Task ExistingInstallationCanSelectItsFirstAdministrator()
    {
        await using var factory = new PublicServicesFactory();
        await factory.InitializeDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        const string owner = "existing-staff@example.test";
        var created = await users.CreateAsync(new IdentityUser { UserName = owner, Email = owner, EmailConfirmed = true });
        Assert.True(created.Succeeded);
        var accounts = scope.ServiceProvider.GetRequiredService<StaffAccounts>();
        Assert.Null(await accounts.PromoteFirstAdminAsync(owner, CancellationToken.None));
        Assert.NotNull(await accounts.PromoteFirstAdminAsync(owner, CancellationToken.None));
        Assert.True(await users.IsInRoleAsync((await users.FindByEmailAsync(owner))!, StaffAccess.Administrator));
    }

    [Fact]
    public async Task StaffMigrationPreservesExistingPublicData()
    {
        await using var factory = new PublicServicesFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PublicData>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261122092234_InitialPublicServices");
        db.Enquiries.Add(new Enquiry
        {
            Id = Guid.NewGuid(), Category = "general", Name = "Existing enquiry", Email = "existing@example.test",
            Message = "Stored before Phase 2.", ConsentVersion = "test-v1", ReceivedAt = 1
        });
        await db.SaveChangesAsync();

        await migrator.MigrateAsync();
        Assert.Equal("Stored before Phase 2.", (await db.Enquiries.AsNoTracking().SingleAsync()).Message);
        Assert.Empty(await db.Users.AsNoTracking().ToListAsync());
    }

    private static async Task InviteFirstAsync(PublicServicesFactory factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var staff = scope.ServiceProvider.GetRequiredService<StaffAccounts>();
        Assert.Null(await staff.InviteAsync(email, firstAccount: true, CancellationToken.None));
        Assert.NotNull(await staff.InviteAsync("another@example.test", firstAccount: true, CancellationToken.None));
    }

    private static Task ChoosePasswordAsync(HttpClient client, string link, string password) =>
        AssertRedirectAsync(SubmitAsync(client, link,
            new() { ["Form.Password"] = password, ["Form.ConfirmPassword"] = password },
            postTo: "/staff/set-password"), "/staff/login");

    private static Task SignInAsync(HttpClient client, string email, string password) =>
        AssertRedirectAsync(SubmitAsync(client, "/staff/login",
            new() { ["Form.Email"] = email, ["Form.Password"] = password }), "/staff");

    private static async Task SignOutAsync(HttpClient client)
    {
        var home = await new HtmlParser().ParseDocumentAsync(await client.GetStringAsync("/staff"));
        var token = home.QuerySelector("form input[name='__RequestVerificationToken']")!.GetAttribute("value")!;
        await AssertRedirectAsync(PostAsync(client, "/staff?handler=SignOut",
            new() { ["__RequestVerificationToken"] = token }), "/staff/login");
    }

    private static async Task UploadMediaAsync(HttpClient client, string kind, string title, string fileName,
        byte[] bytes, Dictionary<string, string> overrides)
    {
        var fields = await ReadFieldsAsync(client, "/staff/media");
        fields["Form.Kind"] = kind;
        fields["Form.Title"] = title;
        foreach (var (key, value) in overrides)
            fields[key] = value;

        using var multipart = new MultipartFormDataContent();
        foreach (var (key, value) in fields)
            multipart.Add(new StringContent(value), key);
        multipart.Add(new ByteArrayContent(bytes), "Upload", fileName);
        await AssertRedirectAsync(client.PostAsync("/staff/media", multipart), "/Staff/Media");
    }

    private static async Task<PublicAsset> ReadAssetAsync(PublicServicesFactory factory, string title)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PublicData>().Assets.AsNoTracking()
            .SingleAsync(item => item.Title == title);
    }

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, string address,
        Dictionary<string, string> values, string? postTo = null)
    {
        var fields = await ReadFieldsAsync(client, address);
        foreach (var (key, value) in values)
            fields[key] = value;
        return await PostAsync(client, postTo ?? address, fields);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string address, Dictionary<string, string> fields)
    {
        using var content = new FormUrlEncodedContent(fields);
        return await client.PostAsync(address, content);
    }

    private static async Task<Dictionary<string, string>> ReadFieldsAsync(HttpClient client, string address)
    {
        var doc = await new HtmlParser().ParseDocumentAsync(await client.GetStringAsync(address));
        var form = doc.QuerySelectorAll("form").Last();
        var values = new Dictionary<string, string>();
        foreach (var node in form.QuerySelectorAll("input[name]:not([type=checkbox]):not([type=file]), textarea[name]"))
            values[node.GetAttribute("name")!] = node.LocalName == "textarea" ? node.TextContent : node.GetAttribute("value") ?? "";
        return values;
    }

    private static async Task AssertRedirectAsync(Task<HttpResponseMessage> request, string location)
    {
        using var response = await request;
        Assert.True(response.StatusCode == HttpStatusCode.Redirect,
            $"Expected redirect to {location}, got {(int)response.StatusCode}: {await ReadErrorAsync(response)}");
        Assert.StartsWith(location, new Uri(new Uri("http://localhost"), response.Headers.Location!).PathAndQuery,
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Redirect)
            return null;
        var doc = await new HtmlParser().ParseDocumentAsync(await response.Content.ReadAsStringAsync());
        return doc.QuerySelector(".form-errors")?.TextContent;
    }

    private static HttpClient CreateClient(PublicServicesFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string ReadLink(string text) => new Uri(Regex.Match(text, @"https?://\S+").Value).PathAndQuery;
}
