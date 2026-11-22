# Tshwane SPCA Website — Final Development Specification

**Version:** 1.0 | **Date:** 22 November 2026 | **Status:** Developer-approved technical scope; SPCA content/launch approval outstanding

## 1. Objective and source of truth

Build and deploy a responsive Tshwane SPCA public website based on the supplied final HTML concept, followed by a small secure administration portal for 2–5 SPCA staff members. Preserve existing SPCA branding, text, structure, logos, original imagery and approved documents. Prioritise a working public preview, affordable hosting, manageable operations and minimal architectural complexity.

**Source material (place in `/reference/`):**
- `FINAL_TSHWANE_SPCA_WEBSITE 10 NOV 2026(1).html` — supplied ~24 MB self-contained HTML concept; contains embedded data-URI images/documents and existing JavaScript.
- `Tshwane_SPCA_Newsletter_August_October_2026_(1).pdf` — 6-page newsletter, August–October 2026; publish as the real PDF download, not a reconstructed HTML document.
- Email thread supplied in project brief, 9–18 November 2026; includes contact, branch, newsletter and Sponsor a Kennel information, and requests feedback on hosting/go-live.

**Authority:** The user's 15 architecture/scope decisions below are confirmed for implementation planning. The attached website is a *proposed* design; do not claim final organisation/legal/business approval. Actual hosting credentials, DNS control, mail sending arrangements, third-party link approvals and any changed banking details require verification before production.

## 2. All 15 confirmed decisions

| # | Area | Confirmed decision |
|---|---|---|
| 1 | Content ownership | Build a simple staff admin portal, not a general-purpose CMS. |
| 2 | Admin-managed modules | Newsletters; photos/galleries; structured website text; events/announcements; forms/documents; donation information. No individual adoptable-animal profiles. |
| 3 | Admin access | 2–5 individually authenticated administrators, all with equal content permissions. |
| 4 | Upload storage | Files stored on the website server on persistent storage, not external Google Drive/blob storage. |
| 5 | Data/migrations | SQLite with EF Core, .NET Aspire for local orchestration, and a dedicated migration workflow/service. |
| 6 | Hosting | Linux VPS on a DigitalOcean Droplet. Existing domain and email hosting should be preserved where possible. |
| 7 | Outbound emails | General enquiries; volunteer applications; newsletter subscriptions; donation/sponsorship enquiries; admin account/invitation/reset mail. |
| 8 | Newsletter | Store subscribers in SQLite; admin list + CSV export; implement consent, double opt-in and unsubscribe. |
| 9 | Enquiry forms | Save submissions in SQLite and notify staff by email. No public visitor attachments. |
| 10 | Enquiry admin | Read-only list, search and filtering. No statuses, assignments or editable follow-up workflow. |
| 11 | Events | Events and announcements with dates, locations, images/posters, and accessible archive. No event registration. |
| 12 | Editing | Fixed website layout, predefined editable fields; no page builder/rich-text layout editor. |
| 13 | Donations | Display bank/EFT details and existing sponsorship instructions; no payment gateway. |
| 14 | Deployment | GitHub Actions automated build/test/deploy to DigitalOcean. |
| 15 | Release | Build in phases, starting with a public-site preview. Introduce full administration afterward. |

## 3. Source-derived public website scope

The supplied HTML has these exact section IDs. Reuse the text, artwork and information unless it is clearly a demo or broken link:

| Anchor | Current source content | MVP action |
|---|---|---|
| `#home` | Hero, logo, four photo tiles, CTA | Preserve visual hierarchy and branding. |
| `#about` | Organisation history, mission, promise | Preserve approved draft wording for stakeholder review. |
| `#adoptions` | Adoption information and poster | Informational only; no animal catalogue. |
| `#process` | Adoption steps | Preserve. |
| `#happy` | Adoption success images | Preserve images, provide alt text. |
| `#clinic` | Clinics overview, price list and booking information | Preserve with verification marker for current prices/bookings. |
| `#donations` | Ways to give | Informational/EFT only. |
| `#sponsor-kennel` | R200/month kennel sponsorship artwork and contact | Keep information; verify amount before go-live. |
| `#banking` | Bank/EFT information | Keep from approved material, explicitly confirm with SPCA pre-launch. |
| `#wish-list` | Donations and items needed | Preserve. |
| `#cruelty` | Complaint information/mailto | Preserve the official reporting route and prominent emergency guidance; no new complaint submission form in approved scope. |
| `#emergency` | Emergency telephone numbers | Make tap-to-call prominent and available on mobile. |
| `#volunteer` | Volunteer instructions, downloadable forms | Preserve; public volunteer enquiry form is separate from existing PDF application downloads. |
| `#newsletter` | Existing newsletter and mailto signup | Replace fake/embedded HTML download with genuine supplied PDF; phased signup implementation. |
| `#charity-shop` | Waltloo charity shop/bookshop | Preserve. |
| `#contact` | Waltloo and Centurion details and Facebook | Preserve verified links. |

The existing HTML also has internal anchors including `#clinic-prices`, `#clinic-booking` and `#volunteer-forms`. Preserve and test these nested navigation targets. Its only existing HTML `<form>` is a newsletter form that opens a mailto client; it is **not** an operational subscription backend.

**Source reference contacts:** Waltloo (012) 803 5219; Centurion (012) 664 5644; after hours Waltloo 083 453 3863, Centurion 082 807 5671. Existing HTML includes `admin@spcapta.org.za`, `centurion@spcapta.org.za`, `chairperson@spcapta.org.za`, and `inspectorcontroller@spcapta.org.za`; newsletters use `newsletter@spcapta.org.za`. Confirm routing with the SPCA before enabling emails. Existing Facebook URL: `https://www.facebook.com/share/1H6FTPK1dH/`.

**Newsletter:** The PDF contains water relief, Gin Squad food donation, Everest Wealth, adoption success, Hammanskraal outreach, grooming, charity shop, donation appeal and bank details. Keep it intact and downloadable. Avoid auto-extracting newsletter narrative as live website news without editorial approval.

## 4. Phasing and acceptance

### Phase 1A — Public website preview (FIRST Codex milestone)
- Create .NET 10 ASP.NET Core Razor Pages web project and Aspire AppHost/ServiceDefaults; choose compatible supported Aspire packages, no unnecessary microservices.
- Convert the existing HTML to maintainable Razor layout/components/CSS/JS while faithfully preserving design, section order, copy, imagery and anchor links. Avoid wholesale redesign.
- Extract original base64/data-URI assets into `wwwroot/assets/` with descriptive filenames and a reference mapping; keep originals untouched in `reference/`. Validate media signatures and downloads; do not execute embedded files or blindly trust extensions.
- Serve the exact supplied six-page PDF at a stable static URL (e.g. `/documents/tshwane-spca-newsletter-2026-08-to-10.pdf`).
- Make navigation, responsive dropdowns, visible tel/mailto links, downloads and optional lightboxes work at desktop and mobile widths.
- No database-backed admin, forms or outgoing production mail required to deliver *this initial preview*. Do not present fake form submission success. Clear labels or mailto fallback for deferred services.
- Include tests/smoke checks for major anchors, PDF HTTP response + content type, asset references, and public route rendering.
- Obtain stakeholder review of content, branch information, clinic prices and donation bank details **before public production launch**.

### Phase 1B — Public-site functionality (after visual/content preview)
- SQLite/EF Core baseline with migrations and persistent data folder.
- General, volunteer and donation/sponsorship enquiry forms; validation, antispam, privacy consent; save submissions then queue/send notification reliably.
- Newsletter signup, confirmation email, unsubscribe and subscriber persistence (admin export may arrive with Phase 2 admin).
- Public events/announcements list/detail/archive endpoints and data-backed content integration; initially seed sample-approved content or show an honest empty state.
- Send dev mail through Mailpit; production provider via HTTPS mail API behind an abstraction. No visitor file attachments.

### Phase 2 — Staff portal
- 2–5 named staff accounts, individual authentication, equal content permissions, no public registration. Invite/reset secure email flows, lockout and CSRF protection.
- Modules: newsletter PDF upload/list/archive, photo galleries, structured text fields, events/announcements, document management, donation information.
- Read-only enquiries list with search/filter; newsletter subscriber list with CSV export (authorisation enforced), unsubscribe/consent fields; optional basic audit log recommended.
- Strict upload validation (allowed images/PDF only as relevant; max sizes; random stored filenames; file-type signature checks; reject executable formats; safe paths); public assets outside sensitive/private folders.
- Publishing status/preview as needed, with no arbitrary HTML/script in editable fields.

### Phase 3 — Production launch and operations
- GitHub Actions CI/CD to DigitalOcean, TLS, reverse proxy, health checks, minimal downtime, verified backups, restore test, DNS cutover and mail provider configuration.
- Production secrets outside Git; migration preflight and rollback procedure; monitored errors and disk space.
- Post-launch handover instructions for staff updates and your operational checklist.

**Explicitly out of scope:** live adoption animal listings, public animal-cruelty report submission, online card payments, ticketing or event registrations, general CMS/page builder, visitor attachments, customer/user logins, staff assignments and two-way CRM.

## 5. Architecture

**Development:** `TshwaneSPCA.AppHost`, `TshwaneSPCA.ServiceDefaults`, `TshwaneSPCA.Web`, `TshwaneSPCA.Data`, and `TshwaneSPCA.MigrationService` (introduced when DB is needed). Optional `TshwaneSPCA.Tests`. Use Razor Pages/partial components and thin application services; do **not** create an extra API unless a real consumer requires one. EF Core SQLite, ASP.NET Core Identity. Aspire orchestrates Web, Mailpit and one-time migration task locally; SQLite is a file, not a SQL container.

**Production:** Prefer a *single* web process or container on the Droplet, with reverse proxy (Caddy or nginx), persistent bind-mounted `/var/lib/tshwane-spca/data/` for SQLite and `/var/lib/tshwane-spca/uploads/` for files, and log/backup destinations outside deployment roots. A dedicated one-shot migration step runs before the web app is switched live; do not run concurrent schema writers. Aspire AppHost is not itself required to run continuously in production. Docker Compose can be an implementation detail even when GitHub Actions is the user-selected deployment mechanism.

**Migration lifecycle:** `dotnet ef migrations add` is a developer authoring operation; applying a checked-in migration is the deploy operation. Configure design-time factory or documented CLI, startup ordering, idempotent seed, backups before upgrades. Use EF Core migrations (`MigrateAsync` / migration bundle), never `EnsureCreated` for a migrated production database. SQLite migrations may rebuild tables; carefully test against a copy and verify restore.

**Data model (proposed, not source-derived):**
- `ApplicationUser` (Identity user; no self-registration)
- `SiteContent` (stable key, text value, locale if later needed, modified by/on)
- `MediaAsset` (id, type, stored path, original filename, size, media type, alt text, published status, created on)
- `Document` / `Newsletter` (title, issue dates, asset reference, publish status)
- `EventAnnouncement` (type, title, summary/body as safe text, start/end datetime, location, hero media, published status, created/modified)
- `Enquiry` (category, name, email, phone optional, message, submitted time, privacy consent, email notification state; read-only in admin)
- `NewsletterSubscriber` (normalised unique email, pending/confirmed/unsubscribed state, consent time/source, confirmation/unsubscribe token hashes and expiries)
- `EmailOutbox` (template/type, payload or structured recipient reference, attempts, next retry, state, timestamps; secrets/tokens never logged)
- `AuditEntry` (optional but useful for bank detail or content changes)

Prefer UTC storage, display South Africa local time in the admin UI. Money is informational only. Avoid copying sensitive data into logs, exports, repositories or test fixtures. Define retention and deletion policies with the organisation.

## 6. Email architecture and delivery

**Important:** DigitalOcean officially blocks outbound SMTP ports 25, 465 and 587 on Droplets. Do not assume the existing mail host can be reached by normal SMTP from the VPS. Use a transactional provider's **HTTPS API over 443** for production notifications. Mailpit/SMTP is for local Aspire development (or an explicitly verified alternative route). The SPCA's existing email **inboxes and MX records remain unchanged**.

Implement `IEmailSender` (dev Mailpit and production HTTPS implementation), named templates for contact/volunteer/donation enquiry, newsletter confirmation/unsubscribe/admin reset, a route/category-to-recipient mapping in validated configuration, and `Reply-To` as the visitor's address when permitted. Prevent header injection; enforce rate limits, honeypot/antibot protection, and server-side validation. Save enquiries/subscriptions before queuing email; idempotent outbox dispatch with retries, error logging without exposing personal data; background service hosted within Web is sufficient on a single VPS. Verify sender domain and SPF/DKIM/DMARC with chosen provider; never hardcode provider keys.

## 7. DNS and domain transition

Current named domain: `spcapta.org.za`. User says authoritative DNS is managed by existing hosting platform; it is **not yet established** whether individual DNS records can be edited without moving nameservers.

1. Identify current registrar, authoritative nameservers and who has access to the authoritative zone; export complete records first.
2. Check existing root/www A/AAAA/CNAME, MX, SPF, DKIM, DMARC, autodiscover, mail subdomains, verification records and any existing app records.
3. Prefer changing *only* the root `A` / suitable `AAAA` and `www` record to DigitalOcean's public IP. Do not blindly overwrite mail-related or TXT records. If the provider will not permit edits, plan a coordinated nameserver transfer with a **complete** zone copy and verified email continuity.
4. Prepare site/TLS/health endpoints before cutover; reduce TTL beforehand where feasible; verify both apex and www and email delivery afterward.
5. Keep a rollback record of previous website records and who authorises the change.

## 8. Security, privacy and reliability

- HTTPS only, secure cookies, Identity password hashing, admin auth rate limiting/lockout, CSRF protection, restrictive file upload policies, no open registration.
- Structured editing: encode plain text, restrict allowed URLs, avoid arbitrary HTML and XSS from announcements.
- Public forms: anti-spam, server-side field length limits, email validation, duplicate rate control, accessible error messages and consent.
- POPIA: privacy notice, purpose limitation, double opt-in, unsubscribe, minimum stored personal data, access controls, retention, data export/deletion process to be approved by SPCA.
- Back up **SQLite DB + WAL/SHM considerations** using SQLite-consistent backups, uploads and configuration. Test restore, schedule off-server encrypted backup, monitor size/free disk. Droplet snapshots alone are not a complete backup strategy.
- Deployment: repeatable build/tests, run migrations once, health check, persistent volume mounts, non-root service, firewall 22 restricted + 80/443, unattended security patch policy, no credentials in GitHub logs.
- Asset/legal: logo/photos/text and donation/clinic details require SPCA sign-off; use supplied originals, not stock substitution. Avoid publishing exact bank details as unverified without confirmation.

## 9. GitHub Actions CI/CD plan

On PR: restore, build, test, basic static validation, dependency audit as feasible; no production secrets. On protected main/tag after required approvals: build container/packaged release, publish to registry, SSH/deploy via protected repository environment, back up DB, apply EF migrations once, restart or swap web deployment, smoke test, rollback binaries if health fails (schema rollback is separate and manual/backup-driven). Use pinned action versions and scoped tokens/secrets. Store persistent data outside project checkout and container image. Do not commit `.db`, secret settings, uploaded files, or extracted sensitive metadata.

## 10. Sign-off / unknowns (do not invent)

- SPCA final acceptance of supplied layout, logo, source photos, copy and use of newsletter content.
- DNS provider and ability to edit existing zone; Droplet public IP; whether root/www target includes redirect preferences.
- Existing mailbox hosting provider and correct routing addresses for each form category.
- Which HTTPS mail API provider and verified From address; appropriate domain validation records.
- Confirm branch telephone numbers, after-hours numbers, contact email destinations, clinic pricing/bookings, donation bank account/branch code, and kennel sponsorship amount against current SPCA authority.
- Who will approve publishing and who will own DigitalOcean/GitHub/domain credentials and backups.
- Privacy/POPIA wording, data retention and email newsletter consent wording.

## 11. Definition of done

**Phase 1A preview:** local Aspire app launches, no console/build errors, visual fidelity to supplied HTML, all original major sections/assets present, no broken menu anchors, downloadable valid PDF, viewport checks at 375/768/1440 px, keyboard navigation and accessibility basics, reproducible README.

**Later production:** forms persist/enqueue/notify reliably, newsletter opt-in/out works, secure staff login, admin modules manage only allowed content, uploads have validated storage, migrations preserve data, backups/restore tested, deployment automation works, TLS/DNS and email deliverability verified, SPCA reviews live content.

## 12. References for implementation verification

- DigitalOcean SMTP policy: https://docs.digitalocean.com/support/why-is-smtp-blocked/
- Aspire EF Core migrations: https://learn.microsoft.com/en-us/dotnet/aspire/database/ef-core-migrations
- EF Core applying migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying
