# Tshwane SPCA website preview

The Phase 1A public website uses .NET 10, ASP.NET Core Razor Pages and Aspire. It preserves the supplied site’s branding, photography, copy and page structure for SPCA review.

## Run locally

Install the .NET 10 SDK, then run from the repository root:

```sh
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet run --project TshwaneSPCA.AppHost --no-build
```

Open the Aspire dashboard URL printed by the AppHost, then open the **web** resource’s HTTP endpoint on port 5081. Aspire starts the web project and checks its `/health` endpoint. This preview uses local HTTP and needs no containers, database, credentials or mail server.

To run just the public site:

```sh
dotnet run --project TshwaneSPCA.Web -- --urls http://localhost:5080
```

In the prepared cloud environment, activate the .NET SDK using the environment’s start instructions before running these commands. The AppHost needs writable Aspire and DCP state directories in your user profile. In an environment that uses an HTTP proxy, include the loopback addresses `localhost`, `127.0.0.1`, and `::1` in the proxy exceptions. In the cloud validation run, DCP used IPv4 via `DCP_IP_VERSION_PREFERENCE=IPv4`. The standalone Web command also works in the restricted shell.

## Project structure

- `TshwaneSPCA.AppHost` starts the preview in Aspire.
- `TshwaneSPCA.ServiceDefaults` provides the health check used by Aspire.
- `TshwaneSPCA.Web` contains the shared Razor layout, navigation, individual section partials, CSS, JavaScript, and static media.
- `TshwaneSPCA.Tests` verifies the public HTTP response, section links, accessible markup, asset types and checksums, and newsletter PDF integrity.
- `reference/` contains the supplied HTML and original newsletter PDF. See the [source inventory](docs/source-inventory.md) and [asset manifest](docs/asset-manifest.json) for the migration mapping.

The newsletter signup button opens a local mail client with a prepared request. The visitor sends it themselves. Volunteer application PDFs and the original six-page newsletter are available as direct downloads.

See the [roadmap](ROADMAP.md) for later features and the approval needed before publication.
