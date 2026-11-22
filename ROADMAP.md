# Roadmap

## Phase 1A review

Review the public preview at desktop and mobile sizes with the SPCA. Verify the logo, photography, copy, branch and after-hours details, email routing (including newsletter requests), clinic prices and booking information, banking details, kennel sponsorship pricing, Facebook link, and permission to publish the documents. The preview follows the supplied HTML for draft contact and donation information.

## Phase 1B public services delivered

SQLite migrations, validated enquiry forms, an email outbox, newsletter confirmation and unsubscribe, and event and announcement pages are implemented. Aspire runs Mailpit for development email. Configure the HTTPS provider and verify SPCA-approved routing, consent wording and retention before enabling live submissions. The events pages show an empty state until approved content is available.

## Phase 2 staff portal delivered

Staff sign in at `/staff` after accepting an emailed invitation. The portal provides password resets, staff account management, fixed website copy and donation details, newsletter and document PDFs, photo publishing, events and announcements, read-only enquiry search, and subscriber CSV export. Prepare the first staff account with the invitation command in the README. Review public content and configure the live mail provider before launch.

## Phase 3 production

Prepare the DigitalOcean deployment, persistent storage, automated releases, tested backups, monitoring and rollback. Confirm DNS control and existing mailbox hosting before planning cutover. See the [development specification](docs/TSHWANE_SPCA_SPEC.md) for scope and acceptance criteria.
