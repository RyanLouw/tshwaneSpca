# Source inventory

Baseline: `FINAL_TSHWANE_SPCA_WEBSITE 10 NOV 2026(1).html`.

## Section anchors

- `#home`
- `#about`
- `#adoptions`
- `#process`
- `#happy`
- `#clinic`
- `#donations`
- `#sponsor-kennel`
- `#banking`
- `#wish-list`
- `#cruelty`
- `#emergency`
- `#volunteer`
- `#newsletter`
- `#charity-shop`
- `#contact`

## Nested anchors and controls

- `#clinic-prices`
- `#clinic-booking`
- `#volunteer-forms`
- `#newsletter-subscribe-form`
- `#newsletter-email`
- `#newsletter-consent`
- `#newsletter-signup-status`

## Link targets

- `#home`
- `#about`
- `#adoptions`
- `#process`
- `#happy`
- `#clinic`
- `#clinic-prices`
- `#clinic-booking`
- `#donations`
- `#banking`
- `#wish-list`
- `#cruelty`
- `#emergency`
- `#sponsor-kennel`
- `#volunteer`
- `#volunteer-forms`
- `#newsletter`
- `#charity-shop`
- `#contact`
- `tel:+27128035219`
- `tel:+27126645644`
- `mailto:centurion@spcapta.org.za`
- `mailto:admin@spcapta.org.za`
- `mailto:chairperson@spcapta.org.za?subject=Sponsor%20a%20Kennel%20Enquiry`
- `mailto:inspectorcontroller@spcapta.org.za?subject=Animal%20Cruelty%20Complaint`
- `tel:+27834533863`
- `tel:+27828075671`
- `mailto:admin@spcapta.org.za?subject=Volunteer%20Application`
- `mailto:chairperson@spcapta.org.za?subject=Volunteer%20Application`
- `mailto:admin@spcapta.org.za?cc=chairperson%40spcapta.org.za&subject=Completed%20Volunteer%20Application`
- `https://www.facebook.com/share/1H6FTPK1dH/`

## Embedded assets

34 references are mapped individually in [asset-manifest.json](asset-manifest.json). The manifest records the original URI and decoded SHA-256 digests, MIME type, owning section and final file. Repeated binary assets share a URL. The embedded HTML newsletter is superseded by the supplied six-page PDF.

## Interactions and approval

- The source JavaScript handled menu disclosure, poster enlargement, account-number copying and a newsletter mailto form. The preview moves these behaviors into a standalone module and uses a native poster dialog.
- Newsletter signup opens a local email client. Sending the message is a separate action. The preview does not save a subscription.
- Contact routes, clinic prices and bookings, banking details, sponsorship terms and external links are draft content pending SPCA review.
