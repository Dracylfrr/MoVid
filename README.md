# MoVid for Windows

Project website and TMDB application URL: **https://github.com/Dracylfrr/MoVid**

A lightweight, native Windows desktop app for personal movie and TV release reminders. Each person keeps their own follows. No paid plan, browser extension, Electron runtime, or AI is required. The simple email experience uses an owner-operated free-tier service; advanced desktop-only personal use can still run without that service.

**Email activation is not live yet.** The email pathway is implemented and tested with simulated delivery. The app owner must complete [the one-time free setup](server/OWNER-SETUP.md) and build against that service URL before real verification or release emails can be sent. The unconfigured installer displays that limitation instead of claiming delivery.

## Install

Run **MoVid-Setup-0.3.0.exe**. It installs for your Windows user without requiring administrator access. **Start MoVid when I sign in to Windows** is selected by default; you can turn it off in setup, Settings, or the tray menu. Startup launches quietly into the system tray.

Open MoVid from Start or the tray. Closing the window keeps checks running. Right-click the tray icon and select **Quit MoVid** to stop completely. Uninstalling removes the startup registration and stops the app. Personal follows and backups remain in `%LOCALAPPDATA%\MoVid\Data` so reinstalling can restore them; delete that folder manually if you want to erase them.

The app targets Windows 10/11 with .NET Framework 4.8, normally included with current installations. It uses the framework already on Windows rather than shipping an embedded browser engine. The executable is unsigned; this local build has no purchased code-signing certificate.

## First use

1. In an email-activated build, enter your email and the six-digit code sent to it. Users do not register for TMDB or configure a sender. The app owner configures those once using [the activation guide](server/OWNER-SETUP.md). Without activation, desktop-only users can open **Advanced personal setup** and connect their own TMDB key; existing personal connections are preserved.
2. Search an actor or filmmaker by name. Press Enter or Search, choose the matching person/photo, and select Follow.
3. MoVid checks upcoming work immediately and sends a first-follow test alert. It clearly says if the database has no future date or project yet.
4. New profiles default to **one day before release at 9 AM local time**. Release-day reminders, both days, trailers, and date confirmations are optional in Settings. Existing profiles keep their saved choices. Movie dates use your selected country.

Following supports **Recently added** (newest first) and **A–Z**. Settings offers **Fast suggestions**, an offline dropdown using names cached from previous searches and follows. No full actor database is downloaded. Enter/Search still finds people online if they are not cached.

## Email and Gmail priority

Settings includes email verification, disconnect, a test-email button, and **Request high-priority emails**. Release-email subjects use the message itself with a `[MoVid]` prefix, such as `[MoVid] Ariana Grande’s [Title] is releasing TOMORROW.` Email-only reminders work even when desktop popups are disabled.

The priority option adds standard sender headers where supported. **It does not set Gmail's Important label and cannot guarantee an iPhone notification.** The app's **Gmail high-priority setup** guide explains creating a Gmail filter from a real received test message and choosing **Always mark it as important**. Test again with your phone's existing High priority only setting. MoVid does not read or change your Gmail inbox.

The service limits mail to the address verified by that device, deduplicates alerts, provides unsubscribe links, and caps total sending at 200 emails/day across the service. It does not buy credits or upgrade a plan. Sender approval, free hosting quotas, provider availability, and spam filtering still apply. Release checks still run on the user's computer; no remote future emails are scheduled after the app quits or uninstalls.

## Free phone lock-screen alerts (optional)

These are internet push notifications through **ntfy**, not SMS, iMessage, email, or the phone's default Messages app. No phone number, paid subscription, payment details, or SMS API is used.

1. Install the free ntfy app on your iPhone or Android phone and allow lock-screen notifications. Official links: <https://docs.ntfy.sh/subscribe/phone/>.
2. In MoVid Settings, choose **Create connection** and copy the generated topic.
3. In ntfy, add a subscription using server `https://ntfy.sh` and exactly that topic.
4. Enable phone alerts in MoVid, save, then choose **Send test alert**.

Your computer must be awake, online, and running MoVid to discover and send changes. The phone needs internet access; normal mobile-data or roaming limits still apply. Phone OS notification permissions, battery restrictions, and the free public service can affect timing. Delivery is not guaranteed and the provider has free usage limits. MoVid does not purchase credits or use paid plans. No third-party service can be promised to remain free forever.

The topic is randomly generated and stored using Windows user encryption. Treat it as a password: anyone knowing it can subscribe or publish. It is not an authenticated private channel. Notification text passes through the public ntfy service and may be cached there. Disconnecting stops future sends; remove the subscription in ntfy too. Quitting or uninstalling MoVid leaves no remotely scheduled future messages; already accepted messages can still arrive.

## Release coverage

Data and images come from TMDB, not IMDb scraping. Historical work is used to identify credits but isn't displayed as watch history. No alerts are generated for dates already reached when someone was followed. Undated projects can stay on your radar; no invented release dates are used.

- Movie theater dates use the earliest limited/wide release in the selected country. Festival dates and physical discs are excluded.
- Digital dates can mean rental, purchase, or streaming. They do not guarantee subscription availability on Netflix or another service.
- TV season premiere dates may reflect the original market and differ from local availability. A followed person must have a confirmed upcoming season credit. Unknown future credits are skipped; episode reminders are not included.
- Recent movies (within one year of their primary date), future movies, and undated projects are scanned. Old-film reissues or very late streaming releases may be omitted.
- Trailer alerts use official YouTube trailers indexed by TMDB. Initial checks establish a baseline without a backlog. Later newly detected trailers may alert after the original trailer publication. Coverage and language availability depend on the database.
- Date-confirmation alerts apply to previously tracked undated titles. Saved/dismissed choices carry over when dates become known.
- Data refreshes approximately daily while the app runs, with reminder checks every five minutes and a manual Check releases button. Missed past-day alerts are not replayed. Title lookup failures preserve last known dates, mark them stale, and pause reminders for those titles.
- Studio follows, default phone Messages, and cross-device profile sync are not included. Email verification is supported after owner activation; it is not a Gmail account login.

## Privacy and cost

Follows and preferences stay in the Windows user profile. TMDB receives searches and requested people/title IDs; photos load from its image server. Optional ntfy delivery receives only notification content and the connection topic. No analytics are included.

The database credential, phone topic, and email session/unsubscribe tokens use Windows DPAPI encryption for the current user. JSON backup exports omit those secrets and the email address. Imports preserve local connections but disable external email/phone delivery until explicitly enabled again. Email activation sends the verified address and notification text to the configured owner service and email provider; TMDB receives catalog queries through that service. See [owner setup and privacy notes](server/OWNER-SETUP.md).

MoVid has no recurring hosting or delivery fees. Free API terms and limits still apply. TMDB's free developer access is for noncommercial use with attribution: <https://developer.themoviedb.org/docs/faq>. This product uses the TMDB API but is not endorsed or certified by TMDB. The TMDB logo and other data/images remain third-party materials outside the source code's MIT license.

## Build from source

Source is published at <https://github.com/Dracylfrr/MoVid> so each person can build/run their own instance.

Requirements: Windows with .NET Framework 4.8's C# compiler and PowerShell. No NuGet, npm, Electron, .NET SDK, or paid tool is needed for the native executable. For the installer, the bootstrap below downloads an unmodified portable NSIS compiler from Tauri's tooling mirror and verifies a pinned SHA-256. Alternatively, obtain NSIS from <https://nsis.sourceforge.io/Download>, extract it inside `tools`, and keep its license files there.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/get-installer-tool.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-installer.ps1
node --test server/worker.test.mjs
```

The installer goes to `dist`; the standalone executable goes to `bin`. The standalone app can run directly; its startup preference registers its current path, so keep it in a permanent location. The installer is the recommended way to keep that location stable.

Tests exercise encrypted storage, reminders, regional dates, duplicate suppression, first follows, trailer/date updates, backups, native UI, cached suggestions, and tray behavior with synthetic data. They use isolated test profiles and do not send real phone alerts or require a real API key. A real-key catalog check and real-phone confirmation remain user setup steps. Tests can render the UI and record memory usage, but don't prove lock-screen delivery on a physical phone.

The email server tests use Node.js 24's in-memory SQLite API to exercise verification, throttling, proxy restrictions, recipient isolation, priority headers, duplicate suppression, unsubscribe, and free sending caps. No real emails or account registrations occur in tests.

## Future macOS release

This version is native Windows Forms to keep Windows lightweight. A macOS build needs a separate native UI and packaging adapter; it cannot be obtained by renaming this executable. The catalog rules and fixtures document the behavior to carry forward.
