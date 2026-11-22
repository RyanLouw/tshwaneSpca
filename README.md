# Tshwane SPCA website

The public website uses .NET 10, ASP.NET Core Razor Pages and Aspire. Phase 1B adds public enquiries, newsletter signup and preferences, and an events and announcements archive to the Phase 1A preview.

## Run locally

Install the .NET 10 SDK and start a Docker-compatible container runtime. From the repository root, run:

```sh
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet run --project TshwaneSPCA.AppHost --no-build
```

In Visual Studio, set **TshwaneSPCA.AppHost** as the startup project and press **F5**. Aspire runs the one-shot SQLite migration, starts Mailpit and starts the site once both dependencies are ready. The dashboard is on port 15120, the website on 5081 and the Mailpit inbox on 8025. Dev mail goes to Mailpit, including the email addressed to a newsletter test visitor. Mailpit requires no provider account or API key. Use fake contact details when testing.

To run the Web project directly, first prepare the SQLite database and run Mailpit independently with SMTP on port 1025 and its inbox on 8025:

```sh
dotnet run --project TshwaneSPCA.MigrationService
docker run --rm --name tshwane-mailpit -p 127.0.0.1:1025:1025 -p 127.0.0.1:8025:8025 axllent/mailpit:v1.27.8
```

Then run `dotnet run --project TshwaneSPCA.Web` in another terminal or start **TshwaneSPCA.Web** in Visual Studio. Its standalone launch profile uses port 5080. Only run one Mailpit instance at a time. On the prepared cloud machine, activate the .NET SDK before running these commands. Aspire also needs writable state in your user profile and direct connectivity to loopback addresses when an HTTP proxy is configured.

## Storage and migrations

Both the Web project and migration runner use the repository's ignored `.data/` directory by default. Set `TSHWANE_DATA_DIR` to the **same absolute persistent directory** for both processes when running outside the repository. The directory holds the SQLite database and Data Protection keys, which are required to decrypt queued mail. Restrict access to that directory and back up the database and keys together.

Run the migration service once, before starting the Web project against a new or upgraded database. Keep it a separate deploy step. For migration authoring:

```sh
dotnet tool restore
dotnet ef migrations add NameOfChange --project TshwaneSPCA.Data --startup-project TshwaneSPCA.MigrationService
```

## Production email configuration

Phase 1B supports Resend over HTTPS. The production default disables email-backed forms until configured. Set these values in the hosting environment after the SPCA approves routing and consent wording:

| Variable | Purpose |
| --- | --- |
| `Mail__Mode` | `Resend` |
| `Mail__PublicUrl` | Canonical HTTPS origin that receives confirmation and unsubscribe links |
| `Mail__From` | Address on the provider-verified sender domain |
| `Mail__GeneralTo` | General enquiry inbox |
| `Mail__VolunteerTo` | Volunteer enquiry inbox |
| `Mail__DonationsTo` | Donation enquiry inbox and fallback for sponsorship |
| `Mail__SponsorshipTo` | Optional Sponsor a Kennel inbox |
| `Mail__WaltlooTo` / `Mail__CenturionTo` | Optional branch inboxes, each falling back to the general inbox |
| `Mail__WaltlooClinicTo` / `Mail__CenturionClinicTo` | Optional clinic inboxes, each falling back to its branch inbox |
| `Mail__ResendApiKey` | API key stored securely in hosting configuration |

Change recipients in the hosting configuration and restart the site. The app uses the visitor's email as the reply address. Local form submissions go to Mailpit using the addresses in `appsettings.Development.json` unless overridden.

The cruelty complaint buttons open a draft in Gmail, Outlook.com, or the visitor’s email app. `Mail__ComplaintTo` sets the destination.

Signed volunteer applications are submitted on the website at `/volunteer/apply`. The form accepts up to three PDFs and a combined limit of 5 MB. The site checks the file extension and PDF markers, gives attachments safe names, and places their bytes in the protected mail queue. It clears queued attachment contents on delivery, expiry, or final failure. The recipient receives the signed files as email attachments. `Mail__VolunteerApplicationsTo` and `Mail__VolunteerApplicationsCc` control the inboxes. Set the CC to an empty string to omit it. Local submissions and attachments appear in Mailpit.

The website saves the enquiry or subscription and queues mail in the same database transaction. Failed mail retries with backoff. Resend requests include a stable idempotency key. If SMTP accepts a message immediately before a process crash, a retry can deliver it twice. Monitor queue failures and agree on data retention before launch. Keep newsletter preference query strings and request bodies out of reverse-proxy logs. For production behind a reverse proxy, configure trusted client IP forwarding at deployment so abuse limits apply per visitor.

The public events pages start empty. The staff publishing interface, subscriber export and campaign sending are separate from this release. See the [roadmap](ROADMAP.md) and [development specification](docs/TSHWANE_SPCA_SPEC.md).

The supplied source HTML and newsletter are in `reference/`. See the [source inventory](docs/source-inventory.md) and [asset manifest](docs/asset-manifest.json) for the Phase 1A migration.
