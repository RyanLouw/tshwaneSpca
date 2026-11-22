var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.TshwaneSPCA_Web>("web", launchProfileName: null)
    .WithHttpEndpoint(port: 5081, targetPort: 5081, env: "ASPNETCORE_HTTP_PORTS", isProxied: false)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
