# MoVid for Windows

Project website and TMDB application URL: **https://github.com/Dracylfrr/MoVid**

A lightweight, native Windows desktop app for personal movie and TV release reminders. Each person installs their own copy and keeps their own follows. No MoVid server, account, paid plan, browser extension, Electron runtime, or AI is required.

## Install

Run **MoVid-Setup-0.2.1.exe**. It installs for your Windows user without requiring administrator access. **Start MoVid when I sign in to Windows** is selected by default; you can turn it off in setup, Settings, or the tray menu. Startup launches quietly into the system tray.

Open MoVid from Start or the tray. Closing the window keeps checks running. Right-click the tray icon and select **Quit MoVid** to stop completely. Uninstalling removes the startup registration and stops the app. Personal follows and backups remain in `%LOCALAPPDATA%\MoVid\Data` so reinstalling can restore them; delete that folder manually if you want to erase them.

The app targets Windows 10/11 with .NET Framework 4.8, normally included with current installations. It uses the framework already on Windows rather than shipping an embedded browser engine. The executable is unsigned; this local build has no purchased code-signing certificate.

## First use

1. Open Settings and connect your free TMDB developer API key or API Read Access Token once. Get one at <https://www.themoviedb.org/settings/api>. For the required **Application URL**, use `https://github.com/Dracylfrr/MoVid`; Settings also has a **Copy application URL** button. Select Desktop Application and describe it as a personal, noncommercial release-reminder app. The URL identifies the project; it is not an API key or authentication callback. A real credential is not bundled with this build and live searches cannot work until one is supplied. This self-run design avoids a shared backend or hidden shared secret.
2. Search an actor or filmmaker by name. Press Enter or Search, choose the matching person/photo, and select Follow.
3. MoVid checks upcoming work immediately and sends a first-follow test alert. It clearly says if the database has no future date or project yet.
4. Choose trailers, date confirmations, day-before reminders, release-day reminders, or both. Movie dates use your selected country; reminder hours use this computer's local time.

Following supports **Recently added** (newest first) and **A–Z**. Settings offers **Fast suggestions**, an offline dropdown using names cached from previous searches and follows. No full actor database is downloaded. Enter/Search still finds people online if they are not cached.

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
- Studio follows, email, default phone Messages, accounts and cross-device profile sync are not included.

## Privacy and cost

Follows and preferences stay in the Windows user profile. TMDB receives searches and requested people/title IDs; photos load from its image server. Optional ntfy delivery receives only notification content and the connection topic. No analytics are included.

The database credential and phone topic use Windows DPAPI encryption for the current user. JSON backup exports omit both secrets. Imports preserve the local database connection and disable phone delivery until explicitly enabled again.

MoVid has no recurring hosting or delivery fees. Free API terms and limits still apply. TMDB's free developer access is for noncommercial use with attribution: <https://developer.themoviedb.org/docs/faq>. This product uses the TMDB API but is not endorsed or certified by TMDB. The TMDB logo and other data/images remain third-party materials outside the source code's MIT license.

## Build from source

Source is published at <https://github.com/Dracylfrr/MoVid> so each person can build/run their own instance.

Requirements: Windows with .NET Framework 4.8's C# compiler and PowerShell. No NuGet, npm, Electron, .NET SDK, or paid tool is needed for the native executable. For the installer, the bootstrap below downloads an unmodified portable NSIS compiler from Tauri's tooling mirror and verifies a pinned SHA-256. Alternatively, obtain NSIS from <https://nsis.sourceforge.io/Download>, extract it inside `tools`, and keep its license files there.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/get-installer-tool.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-installer.ps1
```

The installer goes to `dist`; the standalone executable goes to `bin`. The standalone app can run directly; its startup preference registers its current path, so keep it in a permanent location. The installer is the recommended way to keep that location stable.

Tests exercise encrypted storage, reminders, regional dates, duplicate suppression, first follows, trailer/date updates, backups, native UI, cached suggestions, and tray behavior with synthetic data. They use isolated test profiles and do not send real phone alerts or require a real API key. A real-key catalog check and real-phone confirmation remain user setup steps. Tests can render the UI and record memory usage, but don't prove lock-screen delivery on a physical phone.

## Future macOS release

This version is native Windows Forms to keep Windows lightweight. A macOS build needs a separate native UI and packaging adapter; it cannot be obtained by renaming this executable. The catalog rules and fixtures document the behavior to carry forward.
