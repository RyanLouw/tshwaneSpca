using TshwaneSPCA.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddRazorPages();

var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'none'";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    await next();
});
app.UseStaticFiles();
app.MapRazorPages();
app.MapDefaultEndpoints();
app.Run();

public partial class Program;
