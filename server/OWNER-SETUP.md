# Activate simple MoVid email alerts — owner only

Users should only install MoVid, enter their email, enter its verification code, and follow people. **They do not create TMDB, Cloudflare, or Brevo accounts.** You, as the app owner, connect those services once.

**Current status:** the source implements the pathway, but no owner hosting/sender account has been connected or deployed by this task. The unconfigured installer honestly says email is not activated and cannot send real emails yet.

## 1. Create the free accounts

- **[Cloudflare](https://dash.cloudflare.com/sign-up):** use **Workers Free**, not Workers Paid. The included `workers.dev` address avoids buying a domain. Do not add payment details or paid products.
- **[Brevo](https://www.brevo.com/):** use its **Free** plan. Verify the sender address, complete its sending approval, and create an API key under SMTP/API. You do not need its paid SMS features or email credits. Sender verification and account approval are provider requirements; an unapproved account will not send even if the app is configured.
- **[TMDB](https://www.themoviedb.org/settings/api):** request your noncommercial developer key/read token. Application name: MoVid. Application URL: `https://github.com/Dracylfrr/MoVid`. Keep the credential private.

Do not paste API keys into GitHub issues, source code, chat messages, screenshots, or a public repository.

## 2. Run the guided owner setup

Install free [Node.js LTS](https://nodejs.org/) if it is not already installed. Download this repository's source onto your Windows computer. In the MoVid folder run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/setup-email-service.ps1
```

The script opens Cloudflare's normal sign-in in your browser, creates the small email database, deploys the service, asks for the two API credentials with hidden input, and stores them as Cloudflare secrets. It then builds an installer containing **only the public service URL**, not the credentials. Allow the normal sign-in only for your Cloudflare account. You must complete account creation and any approval yourself.

The setup script does not switch plans, buy credits, request SMS, or enter payment details. Check both provider dashboards remain on Free before running it. The script requires you to confirm that.

If setup was interrupted after creating the database, open `server/wrangler.local.json` and check its `DB` binding has the database name/ID from Cloudflare. Then rerun the script. This local file is ignored by Git. Do not delete a live database to retry setup.

## 3. Confirm actual delivery

Install the newly built **MoVid-Setup-0.3.0.exe**. Enter your own email and request a code. Check Inbox and Spam. Enter the code, follow someone, then choose **Send test email**. Verify the message arrives before distributing that installer to others.

The `/health` endpoint checks configuration presence only; it cannot prove Brevo approved the sender or that an email arrived. No provider credentials or physical phone were available during development, so actual delivery and iPhone lock-screen behavior remain these activation checks.

## Gmail: High priority only

The app includes **Request high-priority emails**. This adds standard `Importance`, `X-Priority`, and `X-MSMail-Priority` headers when enabled. **It does not set Gmail's Important label or guarantee a phone alert.** Gmail controls that classification.

After receiving a MoVid test email:

1. Open Gmail on your computer and select the test email.
2. Choose **More → Filter messages like these**.
3. Create a filter, choose **Always mark it as important**, and save it.
4. Send another MoVid test and check the phone.

Use the actual received sender, because the email provider can rewrite unauthenticated sender domains. All subjects also start with `[MoVid]`. Keep Gmail and iPhone notifications enabled; Focus settings, inbox categories, notification previews, and Gmail's own behavior can still affect alerts.

Google references: [importance markers](https://support.google.com/mail/answer/186543), [filters](https://support.google.com/mail/answer/6579), [iPhone high-priority notifications](https://support.google.com/mail/answer/1075549).

## Free limits and stop behavior

Brevo currently advertises 300 free emails per day. MoVid imposes a lower **200-email daily service-wide cap**, including verification codes. It stops sending at the cap; it does not upgrade the plan. This is suitable for a small initial service, not unlimited users. Other use of the same Brevo account consumes its allowance too. [Brevo Free plan](https://help.brevo.com/hc/en-us/articles/208580669-FAQs-What-are-the-limits-of-the-Free-plan).

Use [Cloudflare Workers Free](https://developers.cloudflare.com/workers/platform/pricing/) and D1 Free. Those services have quotas too. The app cannot promise third-party free plans or availability forever; if quotas or plans change, stop the service or adjust its caps instead of adding paid billing.

The desktop app still discovers releases while the user's computer is running. The server sends only in response to a verified device request; it does not schedule future reminder emails. Closing the window keeps the app in the tray; quitting or uninstalling stops new requests. Already accepted messages can still arrive. A daily cleanup job only deletes expired verification/rate records and old delivery metadata—it sends no mail.

The unsubscribe link stops alerts and revokes all sessions for that address. **Disconnect email** in the app revokes that device. Sessions expire after 30 days of inactivity. No Gmail inbox-reading permission is requested.

## Build a later app version against the same service

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1 -ServiceUrl https://YOUR-WORKER.YOUR-SUBDOMAIN.workers.dev
```

The server URL is public configuration. API keys remain on the server. Never replace that with another person's untrusted service: the operator of the configured service receives verified addresses and release-alert text.

## Test locally without sending email

With Node.js 24 or newer and Windows:

```powershell
node --test server/worker.test.mjs
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1
```

These tests simulate provider responses and use an in-memory database. They do not send real emails, register real addresses, deploy a service, or alter your real startup settings.
