var builder = DistributedApplication.CreateBuilder(args);

const int webPort = 5081;
var webUrl = $"http://localhost:{webPort}";
builder.AddProject<Projects.TshwaneSPCA_Web>("web", launchProfileName: null)
    .WithHttpEndpoint(port: webPort, targetPort: webPort, isProxied: false)
    // Visual Studio can pass ASPNETCORE_URLS, which takes precedence over HTTP_PORTS.
    .WithEnvironment("ASPNETCORE_URLS", webUrl)
    .WithEnvironment("ASPNETCORE_HOSTINGSTARTUPEXCLUDEASSEMBLIES", "Microsoft.WebTools.ApiEndpointDiscovery")
    .WithHttpHealthCheck("/health");

builder.Build().Run();
