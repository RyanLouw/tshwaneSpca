using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TshwaneSPCA.Data;
using TshwaneSPCA.ServiceDefaults;
using TshwaneSPCA.Web.PublicServices;
using TshwaneSPCA.Web.Staff;

var bootstrap = args is ["--invite-first-staff", _];
var promote = args is ["--promote-first-admin", _];
var builder = WebApplication.CreateBuilder(bootstrap || promote ? [] : args);
// Avoid recording newsletter link tokens in HTTP request logs.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.AddServiceDefaults();
builder.Services.AddRazorPages(options => options.Conventions.AuthorizeFolder("/Staff"));
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;
    options.Password.RequiredLength = 12;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<PublicData>().AddDefaultTokenProviders();
builder.Services.AddAuthorization(options =>
{
    foreach (var module in StaffAccess.Modules.Keys)
        options.AddPolicy(module, policy => policy.RequireRole(StaffAccess.Administrator, module));
});
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/staff/login";
    options.Events.OnRedirectToAccessDenied = async context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("You do not have access to this staff section.");
    };
    options.Cookie.Name = "TshwaneSPCA.Staff";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(24));
builder.Services.AddScoped<StaffAccounts>();
builder.Services.AddScoped<SiteCopy>();


var dataDirectory = PublicData.ResolveDirectory(builder.Environment.ContentRootPath,
    builder.Configuration["TSHWANE_DATA_DIR"]);
builder.Services.AddDbContext<PublicData>(options => PublicData.Configure(options, dataDirectory));
builder.Services.AddSingleton(new MediaLibrary(Path.Combine(dataDirectory, "media")));
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
if (bootstrap || promote)
{
    await using var scope = app.Services.CreateAsyncScope();
    var accounts = scope.ServiceProvider.GetRequiredService<StaffAccounts>();
    var result = promote
        ? await accounts.PromoteFirstAdminAsync(args[1], CancellationToken.None)
        : await accounts.InviteAsync(args[1], firstAccount: true, CancellationToken.None);
    Console.WriteLine(result ?? (promote ? "Administrator access granted." : "Staff invitation queued. Start the site to deliver it."));
    Environment.ExitCode = result is null ? 0 : 1;
    return;
}

app.Use(async (context, next) =>
{
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    if (context.Request.Path.StartsWithSegments("/staff"))
        context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/library/file/{id:guid}", async Task<IResult> (Guid id, PublicData database, MediaLibrary storage,
    CancellationToken cancellationToken) =>
{
    var asset = await database.Assets.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.PublishedAt != null,
        cancellationToken);
    if (asset is null || storage.Open(asset) is not { } stream)
        return Results.NotFound();

    return Results.File(stream, asset.ContentType,
        asset.ContentType == "application/pdf" ? MediaLibrary.DownloadName(asset) : null, enableRangeProcessing: true);
});
app.MapGet("/admin", () => Results.Redirect("/staff"));
app.MapRazorPages();
app.MapDefaultEndpoints();
app.Run();

public partial class Program;
