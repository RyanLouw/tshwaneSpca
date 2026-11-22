# Roadmap

## Phase 1A review

Review the public preview at desktop and mobile sizes with the SPCA. Verify the logo, photography, copy, branch and after-hours details, email routing (including newsletter requests), clinic prices and booking information, banking details, kennel sponsorship pricing, Facebook link, and permission to publish the documents. The preview follows the supplied HTML for draft contact and donation information. Its newsletter request opens the draft admin email route pending confirmation of the intended inbox.

## Phase 1B public services

After preview approval, add SQLite and EF Core migrations, validated enquiry forms, an email outbox, newsletter subscriptions with confirmation and unsubscribe, and events and announcements. Add Mailpit to Aspire for development email and configure a transactional HTTPS mail provider for production. Confirm email routing and POPIA/consent wording before enabling public submissions.

## Phase 2 staff portal

Add invite-only staff access and the fixed-layout content, newsletter, gallery, document and events administration. Provide read-only enquiry search and newsletter CSV export.

## Phase 3 production

Prepare the DigitalOcean deployment, persistent storage, automated releases, tested backups, monitoring and rollback. Confirm DNS control and existing mailbox hosting before planning cutover. See the [development specification](docs/TSHWANE_SPCA_SPEC.md) for scope and acceptance criteria.
