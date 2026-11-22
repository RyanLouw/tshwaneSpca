using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace TshwaneSPCA.Tests;

public sealed class PublicPreviewTests(PublicServicesFactory factory)
    : IClassFixture<PublicServicesFactory>
{
    private static readonly string[] SectionIds =
    [
        "home", "about", "adoptions", "process", "happy", "clinic", "donations",
        "sponsor-kennel", "banking", "wish-list", "cruelty", "emergency", "volunteer",
        "newsletter", "charity-shop", "contact"
    ];

    [Fact]
    public async Task HomeRendersAllSectionsAndEveryFragmentHasAUniqueDestination()
    {
        using var client = factory.CreateClient();
        var document = await GetHomePage(client);

        Assert.Equal(SectionIds, document.QuerySelectorAll("main section[id]").Select(section => section.Id));
        Assert.All(new[] { "clinic-prices", "clinic-booking", "volunteer-forms" },
            id => Assert.NotNull(document.GetElementById(id)));

        var ids = document.QuerySelectorAll("[id]").Select(element => element.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(document.QuerySelectorAll("a[href^='#']"), link =>
            Assert.NotNull(document.GetElementById(link.GetAttribute("href")![1..])));
        Assert.Equal(4, document.QuerySelectorAll("#emergency a[href^='tel:'], #contact a[href^='tel:']").Length);
    }

    [Fact]
    public async Task PageHasAccessibleControlsAndLocalAssetsWithExpectedContent()
    {
        using var client = factory.CreateClient();
        var document = await GetHomePage(client);
        foreach (var image in document.QuerySelectorAll("img[src]"))
        {
            Assert.False(string.IsNullOrWhiteSpace(image.GetAttribute("alt")));
        }

        foreach (var control in document.QuerySelectorAll("button[aria-controls]"))
        {
            Assert.NotNull(document.GetElementById(control.GetAttribute("aria-controls")!));
            Assert.Equal("false", control.GetAttribute("aria-expanded"));
        }

        Assert.All(document.QuerySelectorAll("#newsletter form input[required]"),
            input => Assert.NotNull(input.Closest("label") ?? document.QuerySelector($"label[for='{input.Id}']")));

        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "asset-manifest.json")));
        var expectedHashes = manifest.RootElement.EnumerateArray()
            .DistinctBy(asset => asset.GetProperty("url").GetString())
            .ToDictionary(asset => asset.GetProperty("url").GetString()!,
                asset => asset.GetProperty("contentSha256").GetString(), StringComparer.Ordinal);

        var urls = document.QuerySelectorAll("img[src], script[src], link[href], a[download]")
            .Select(element => element.GetAttribute("src") ?? element.GetAttribute("href")!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(expectedHashes.Keys.All(urls.Contains));
        Assert.Empty(document.QuerySelectorAll("[style], [src^='data:'], [href^='data:']"));

        foreach (var url in urls)
        {
            Assert.StartsWith("/", url);
            using var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var contentType = response.Content.Headers.ContentType!.MediaType;
            var contents = await response.Content.ReadAsByteArrayAsync();
            Assert.NotEmpty(contents);
            if (expectedHashes.TryGetValue(url, out var expectedHash))
            {
                Assert.Equal(expectedHash, Convert.ToHexStringLower(
                    SHA256.HashData(contents)));
            }
            Assert.Equal(Path.GetExtension(url) switch
            {
                ".jpg" => "image/jpeg",
                ".png" => "image/png",
                ".pdf" => "application/pdf",
                ".css" => "text/css",
                ".js" => "text/javascript",
                _ => throw new InvalidOperationException($"Unexpected asset type: {url}")
            }, contentType);
        }
    }

    [Fact]
    public async Task NewsletterDownloadMatchesTheSuppliedPdfByteForByte()
    {
        using var client = factory.CreateClient();
        var document = await GetHomePage(client);
        var download = Assert.Single(document.QuerySelectorAll("#newsletter a[download]"));
        using var response = await client.GetAsync(download.GetAttribute("href"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        var pdf = await response.Content.ReadAsByteArrayAsync();
        Assert.True(pdf.AsSpan().StartsWith("%PDF-"u8));
        var source = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "newsletter-source.pdf"));
        Assert.True(pdf.AsSpan().SequenceEqual(source), "The preview must serve the original newsletter bytes.");
    }

    private static async Task<IDocument> GetHomePage(HttpClient client)
    {
        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        return await new HtmlParser().ParseDocumentAsync(await response.Content.ReadAsStringAsync());
    }
}
