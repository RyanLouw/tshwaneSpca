var builder = DistributedApplication.CreateBuilder(args);

const int webPort = 5081;
const int smtpPort = 1025;
var webUrl = $"http://localhost:{webPort}";
var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.27.8")
    .WithEndpoint(port: smtpPort, targetPort: 1025, name: "smtp", scheme: "tcp")
    .WithHttpEndpoint(port: 8025, targetPort: 8025, name: "inbox")
    .WithHttpHealthCheck("/api/v1/info", endpointName: "inbox");

var migrations = builder.AddProject<Projects.TshwaneSPCA_MigrationService>("migrations", launchProfileName: null);

builder.AddProject<Projects.TshwaneSPCA_Web>("web", launchProfileName: null)
    .WithHttpEndpoint(port: webPort, targetPort: webPort, isProxied: false)
    // Visual Studio can pass ASPNETCORE_URLS, which takes precedence over HTTP_PORTS.
    .WithEnvironment("ASPNETCORE_URLS", webUrl)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPNETCORE_HOSTINGSTARTUPEXCLUDEASSEMBLIES", "Microsoft.WebTools.ApiEndpointDiscovery")
    .WithEnvironment("Mail__PublicUrl", webUrl)
    .WithHttpHealthCheck("/health")
    .WaitForCompletion(migrations)
    .WaitFor(mailpit);

builder.Build().Run();
