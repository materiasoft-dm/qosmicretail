---
name: release-pos-apps
description: Build the Mercurius POS apps (Windows zip + Android APK) from Mercurius.Mobile, bump the version, and put them on the live site's "Download Mobile App" page. Use when asked to release, publish or update the mobile/POS app downloads, or to "update the version on the live site".
---

# Release the POS apps to the live download page

The web app's **Download Mobile App** page (`DownloadsController`, `/Downloads`, signed-in users
only) serves two files from `Mercurius/Downloads/`:

- `Mercurius-POS-Windows-x64.zip`: unpackaged, fully self-contained Windows build
- `Mercurius-POS-Android.apk`: Release APK signed with the release key
- plus `version.json` (version/build shown on the page)

That folder is **git-ignored** (the files are ~90–200 MB; the repo is public) and ships only by
publishing the web app from the machine that built them. Releasing = rebuild the files, then
redeploy the web app.

## Prerequisites (this machine)

- `%USERPROFILE%\.mercurius\mercurius-android-release.keystore` + `android-signing.password`:
  the Android release key (alias `mercurius`). **Every release must be signed with this same key**,
  or Android refuses to install the update over the previous version. If it's missing, stop and ask
  the user to restore it from backup. Never generate a new one silently. The password file holds
  the password **twice, on two lines**: apksigner reads line 1 as the keystore password and line 2
  as the key password; one line fails as `java.exe exited with code 2`. The script verifies the
  finished APK's certificate against this key and refuses to continue on a mismatch.
- Android SDK at `%LOCALAPPDATA%\Android\Sdk`, JDK at `C:\Program Files\Android\openjdk\jdk-21.0.8`
  (the script falls back to these when `ANDROID_HOME`/`JAVA_HOME` aren't visible in the session).
- `site1.PublishSettings` at the repo root (git-ignored) holds the Web Deploy password for the
  live site (`darkmaster-002-site1`). Never print it.

## Steps

1. **Check what else would ship.** Deploying publishes the web app *exactly as it is in the working
   tree*, and the site runs pending EF migrations against the live database on startup. Run
   `git status` and look for server changes (`Mercurius/`, `Mercurius.Repo*/`, `Mercurius.Client/`)
   and migrations in `Mercurius.Repo.Migrations.SqlServer/Migrations/` that haven't been deployed.
   If there are any, tell the user what they are (especially migrations: what they change in the
   database) and get an explicit go-ahead before deploying. A release of just the apps needs no
   server changes.

2. **Build both apps** (bumps the build number, which Android requires to go up for updates):

   ```powershell
   powershell -ExecutionPolicy Bypass -File Mercurius.Mobile\publish-downloads.ps1
   ```

   Options: `-Platforms Windows` or `-Platforms Android` (just one), `-DisplayVersion 1.1`
   (change the user-facing version; default keeps it), `-NoVersionBump` (rebuild a failed attempt
   without burning a build number). Run it in the background: the Android Release build can take
   10+ minutes on this OneDrive path. It ends with `Done: Mercurius POS <version> (build <n>)`.

3. **Smoke-test before shipping.**
   - Windows: launch `%TEMP%\mercurius-pos-build\windows\Mercurius POS\Mercurius.Mobile.exe` and
     confirm it stays running and responding for ~10 s, then close it. On the dev machine it shares
     app data with the Debug build (same app id), so it may open already signed in with local test
     data. That's expected; a real terminal starts at the login page.
   - Android (if an emulator/device is attached, `adb devices`): the release APK is signed with a
     different key than Debug builds, so `adb uninstall com.companyname.mercurius.mobile` first
     (wipes that device's app data), then `adb install -r Mercurius\Downloads\Mercurius-POS-Android.apk`,
     launch it, and check `adb logcat -d -s AndroidRuntime:E` shows no `FATAL EXCEPTION`.
   - Release builds always point at the live site (`ApiConfig.cs`), so don't sign in with test
     credentials during a smoke test. That would create real audit rows on live.

4. **Deploy** (only after step 1 is clear):

   ```powershell
   powershell -ExecutionPolicy Bypass -File Mercurius.Mobile\publish-downloads.ps1 -Platforms Windows,Android -NoVersionBump -Deploy
   ```

   That would rebuild everything. To deploy the files you just built without rebuilding, run the
   web publish directly instead (the deploy block at the end of the script, standalone):

   ```powershell
   $xml = [xml](Get-Content "<repo>\site1.PublishSettings")
   $deployPassword = ($xml.publishData.publishProfile | Where-Object { $_.profileName -eq "darkmaster-002-site1 - Web Deploy" }).userPWD
   Set-Location "<repo>\Mercurius"
   dotnet publish -c Release -p:PublishProfile="darkmaster-002-site1 - Web Deploy" -p:Password=$deployPassword -p:AllowUntrustedCertificate=true -v minimal
   ```

   Never name the variable `$pwd` (PowerShell's current-directory variable; it gets overwritten
   and the deploy fails with a misleading 401). The upload includes ~150+ MB of app files, so
   expect several minutes.

5. **Verify live**: `http://darkmaster-002-site1.dtempurl.com/health` reports Healthy, and
   `/Downloads/Windows` and `/Downloads/Android` answer **302** (redirect to login) rather than 404
   when fetched without signing in. That only proves the routes exist; to prove the files landed,
   list the live folder with Web Deploy's read-only dump (password from `site1.PublishSettings`,
   never printed; `publishUrl` already includes `https://`):

   ```powershell
   msdeploy.exe -verb:dump -source:contentPath="darkmaster-002-site1/Downloads",computerName="<publishUrl>",userName="<userName>",password="<userPWD>",authType="Basic" -allowUntrusted -xml
   ```

   (`C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe`, run via `cmd /c`). Expect both
   files with their local sizes plus `version.json`.

6. **Report** the version/build, both file sizes, and the live check results. Remind the user that
   `Mercurius.Mobile/Mercurius.Mobile.csproj` now has the bumped `ApplicationVersion`
   (and `ApplicationDisplayVersion`, if changed); that change should be committed. The built
   files themselves are never committed.
