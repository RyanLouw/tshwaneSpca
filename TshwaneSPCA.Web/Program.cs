using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using TshwaneSPCA.Data;
using TshwaneSPCA.ServiceDefaults;
using TshwaneSPCA.Web.PublicServices;

var builder = WebApplication.CreateBuilder(args);
// Avoid recording newsletter link tokens in HTTP request logs.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.AddServiceDefaults();
builder.Services.AddRazorPages();

var dataDirectory = PublicData.ResolveDirectory(builder.Environment.ContentRootPath,
    builder.Configuration["TSHWANE_DATA_DIR"]);
builder.Services.AddDbContext<PublicData>(options => PublicData.Configure(options, dataDirectory));
builder.Services.AddDataProtection()
    .SetApplicationName("TshwaneSPCA")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "keys")));

var mail = builder.Configuration.GetSection("Mail").Get<MailSettings>() ?? new MailSettings();
if (!mail.IsValid())
    throw new InvalidOperationException("Invalid Mail settings. See the setup instructions.");
builder.Services.AddSingleton(mail);
builder.Services.AddSingleton<OutboxPayload>();
builder.Services.AddScoped<EnquiryQueue>();
if (mail.Mode == "Smtp")
    builder.Services.AddScoped<IEmailSender, MailpitSender>();
if (mail.Mode == "Resend")
    builder.Services.AddHttpClient<IEmailSender, ResendSender>().ConfigurePrimaryHttpMessageHandler(
        () => new HttpClientHandler { AllowAutoRedirect = false });
if (mail.Enabled)
    builder.Services.AddHostedService<OutboxWorker>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("public-form", context => HttpMethods.IsPost(context.Request.Method)
        ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(5) })
        : RateLimitPartition.GetNoLimiter("read"));
});

var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.MapRazorPages();
app.MapDefaultEndpoints();
app.Run();

public partial class Program;
