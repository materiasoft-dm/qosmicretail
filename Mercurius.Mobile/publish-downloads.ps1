# Builds the downloadable POS apps from Mercurius.Mobile and puts them where the web app's
# "Download Mobile App" page (DownloadsController) serves them from: Mercurius/Downloads.
#
#   powershell -ExecutionPolicy Bypass -File Mercurius.Mobile\publish-downloads.ps1            # both, bump build
#   ... -Platforms Windows                  # just one platform
#   ... -DisplayVersion 1.1                 # also change the user-facing version (default: keep)
#   ... -NoVersionBump                      # rebuild without bumping (e.g. a failed attempt)
#   ... -Deploy                             # then publish the web app (with these files) to the live site
#
# What it produces:
#   Mercurius-POS-Windows-x64.zip  - unpackaged, fully self-contained (.NET + Windows App SDK bundled):
#                                    unzip and run, nothing to install, no signing certificate to trust.
#   Mercurius-POS-Android.apk      - signed with the release key in %USERPROFILE%\.mercurius (NOT in the
#                                    repo). Every release must use that same key, or Android refuses to
#                                    install it over the previous version - back that folder up.
#   version.json                   - version/build shown on the download page.
#
# Both are Release builds, so they always talk to the live site (ApiConfig.cs), whatever the Debug
# URL is temporarily set to for local testing. The outputs are git-ignored (~100 MB each).

param(
    [ValidateSet("Windows", "Android")]
    [string[]] $Platforms = @("Windows", "Android"),
    [string] $DisplayVersion,
    [switch] $NoVersionBump,
    [switch] $Deploy
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $PSScriptRoot "Mercurius.Mobile.csproj"
$downloadsDir = Join-Path $repo "Mercurius\Downloads"
$stagingRoot = Join-Path $env:TEMP "mercurius-pos-build"
New-Item -ItemType Directory -Force $downloadsDir | Out-Null

# ------------------------------------------------------------------ version
# ApplicationVersion is the integer build number (Android's versionCode - must only ever go up for
# an update to install over the previous one); ApplicationDisplayVersion is what people see.
$csprojText = [IO.File]::ReadAllText($project)
$build = [int]([regex]::Match($csprojText, '<ApplicationVersion>(\d+)</ApplicationVersion>').Groups[1].Value)
$display = [regex]::Match($csprojText, '<ApplicationDisplayVersion>([^<]+)</ApplicationDisplayVersion>').Groups[1].Value
if (-not $NoVersionBump) { $build++ }
if ($DisplayVersion) { $display = $DisplayVersion }
$updated = [regex]::Replace($csprojText, '<ApplicationVersion>\d+</ApplicationVersion>', "<ApplicationVersion>$build</ApplicationVersion>")
$updated = [regex]::Replace($updated, '<ApplicationDisplayVersion>[^<]+</ApplicationDisplayVersion>', "<ApplicationDisplayVersion>$display</ApplicationDisplayVersion>")
if ($updated -ne $csprojText) { [IO.File]::WriteAllText($project, $updated, (New-Object Text.UTF8Encoding($false))) }
Write-Host "Building Mercurius POS $display (build $build) for: $($Platforms -join ', ')"

# ------------------------------------------------------------------ Windows
if ($Platforms -contains "Windows") {
    $winRoot = Join-Path $stagingRoot "windows"
    $appFolder = Join-Path $winRoot "Mercurius POS"   # the folder name people see after extracting
    if (Test-Path $winRoot) { Remove-Item $winRoot -Recurse -Force }

    dotnet publish $project -f net10.0-windows10.0.19041.0 -c Release `
        -p:RuntimeIdentifierOverride=win-x64 -p:WindowsPackageType=None `
        -p:WindowsAppSDKSelfContained=true -p:SelfContained=true -o $appFolder
    if ($LASTEXITCODE -ne 0) { throw "Windows publish failed (exit $LASTEXITCODE)." }
    if (-not (Test-Path (Join-Path $appFolder "Mercurius.Mobile.exe"))) { throw "Windows publish produced no Mercurius.Mobile.exe." }

    @"
Mercurius POS for Windows - version $display (build $build)

1. Right-click the downloaded zip > Extract All..., and extract it somewhere permanent
   (for example C:\Mercurius POS). Don't run it from inside the zip.
2. Open the extracted folder and double-click Mercurius.Mobile.exe.
3. The first time, Windows may show "Windows protected your PC" because the app isn't
   code-signed. Click "More info", then "Run anyway".
4. Sign in with your Mercurius account, then open Settings (menu) to choose this
   terminal's clinic and give it a name (e.g. "Front Counter").

Tip: right-click Mercurius.Mobile.exe > Send to > Desktop (create shortcut).
To update: extract the new zip over this folder. Sign-in and unsent sales are kept.
"@ | Set-Content -Path (Join-Path $appFolder "READ ME FIRST.txt") -Encoding UTF8

    $zipPath = Join-Path $downloadsDir "Mercurius-POS-Windows-x64.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($appFolder, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $true)
    Write-Host ("Windows: {0} ({1:N1} MB)" -f $zipPath, ((Get-Item $zipPath).Length / 1MB))
}

# ------------------------------------------------------------------ Android
if ($Platforms -contains "Android") {
    $signDir = Join-Path $env:USERPROFILE ".mercurius"
    $keystore = Join-Path $signDir "mercurius-android-release.keystore"
    $passwordFile = Join-Path $signDir "android-signing.password"
    if (-not (Test-Path $keystore) -or -not (Test-Path $passwordFile)) {
        throw "Android release key not found in $signDir. It must be the SAME key as every previous release " +
              "(restore it from backup) - a new key means devices can't update without uninstalling first."
    }
    # apksigner reads BOTH passwords from this one file: line 1 = keystore password, line 2 = key
    # password (the same value for a PKCS12 keystore). With only one line it fails with
    # "end of file reached" (surfacing as 'java.exe exited with code 2').
    if (([IO.File]::ReadAllLines($passwordFile) | Where-Object { $_ }).Count -lt 2) {
        throw "$passwordFile must contain the keystore password on two lines (same password twice)."
    }

    # Don't trust ANDROID_HOME/JAVA_HOME blindly: a long-running session (e.g. an editor started
    # before they were last changed) can carry a stale value - on this machine an older SDK folder
    # without Build-Tools, which fails with XA5205 "Cannot find aapt2.exe". Take the first candidate
    # that actually has what the build needs.
    $sdkCandidates = @($env:ANDROID_HOME, [Environment]::GetEnvironmentVariable("ANDROID_HOME", "User"),
        $env:ANDROID_SDK_ROOT, (Join-Path $env:LOCALAPPDATA "Android\Sdk")) | Where-Object { $_ }
    $sdk = $sdkCandidates | Where-Object { Get-ChildItem (Join-Path $_ "build-tools") -Filter aapt2.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 } | Select-Object -First 1
    if (-not $sdk) { throw "No Android SDK with Build-Tools found (tried: $($sdkCandidates -join '; '))." }

    $jdkCandidates = @($env:JAVA_HOME, [Environment]::GetEnvironmentVariable("JAVA_HOME", "User"),
        "C:\Program Files\Android\openjdk\jdk-21.0.8") | Where-Object { $_ }
    $jdk = $jdkCandidates | Where-Object { Test-Path (Join-Path $_ "bin\javac.exe") } | Select-Object -First 1
    if (-not $jdk) { throw "No JDK found (tried: $($jdkCandidates -join '; '))." }

    # Passed both ways: as properties below and as environment variables for any build step that
    # doesn't see the properties.
    $env:ANDROID_HOME = $sdk
    $env:JAVA_HOME = $jdk
    Write-Host "Android SDK: $sdk; JDK: $jdk"

    $apkOut = Join-Path $stagingRoot "android"
    if (Test-Path $apkOut) { Remove-Item $apkOut -Recurse -Force }

    # Always a clean Android Release build: an incremental one can decide the signed APK from a
    # previous build is "up to date" and skip re-signing - which shipped a debug-signed APK once
    # (confirmed with apksigner). The signature is verified again below regardless.
    foreach ($dir in @("bin\Release\net10.0-android", "obj\Release\net10.0-android")) {
        $full = Join-Path $PSScriptRoot $dir
        if (Test-Path $full) { Remove-Item $full -Recurse -Force }
    }

    # Target ABIs (arm, arm64, x64) are set for Release in Mercurius.Mobile.csproj - passing a
    # semicolon list on the command line gets mangled into a single invalid RuntimeIdentifier.
    dotnet publish $project -f net10.0-android -c Release `
        -p:AndroidPackageFormat=apk `
        -p:AndroidKeyStore=true "-p:AndroidSigningKeyStore=$keystore" -p:AndroidSigningKeyAlias=mercurius `
        "-p:AndroidSigningStorePass=file:$passwordFile" "-p:AndroidSigningKeyPass=file:$passwordFile" `
        "-p:AndroidSdkDirectory=$sdk" "-p:JavaSdkDirectory=$jdk" -o $apkOut
    if ($LASTEXITCODE -ne 0) { throw "Android publish failed (exit $LASTEXITCODE)." }

    $signedApk = Get-ChildItem $apkOut -Filter "*-Signed.apk" | Select-Object -First 1
    if (-not $signedApk) { throw "Android publish produced no signed APK in $apkOut." }

    # Prove the APK carries the release key's certificate before it goes anywhere near the site.
    $buildTools = Get-ChildItem (Join-Path $sdk "build-tools") -Directory | Sort-Object Name -Descending | Select-Object -First 1
    $env:PATH = "$(Join-Path $jdk 'bin');$env:PATH"
    $apkCertLine = & (Join-Path $buildTools.FullName "apksigner.bat") verify --print-certs $signedApk.FullName 2>&1 |
        Select-String "certificate SHA-256 digest" | Select-Object -First 1
    $apkDigest = ($apkCertLine -replace '.*digest:\s*', '').Trim().ToLowerInvariant()
    $keyListing = cmd /c "`"$(Join-Path $jdk 'bin\keytool.exe')`" -list -v -keystore `"$keystore`" -alias mercurius -storepass:file `"$passwordFile`" 2>&1"
    $keyDigest = (($keyListing | Select-String "SHA256:" | Select-Object -First 1) -replace '.*SHA256:\s*', '' -replace ':', '').Trim().ToLowerInvariant()
    if (-not $apkDigest -or $apkDigest -ne $keyDigest) {
        throw "The APK is NOT signed with the release key (APK cert $apkDigest, release key $keyDigest). Not publishing it."
    }
    Write-Host "Android APK signature verified against the release key."
    $apkPath = Join-Path $downloadsDir "Mercurius-POS-Android.apk"
    Copy-Item $signedApk.FullName $apkPath -Force
    Write-Host ("Android: {0} ({1:N1} MB)" -f $apkPath, ((Get-Item $apkPath).Length / 1MB))
}

# ------------------------------------------------------------------ version.json
@{ version = $display; build = $build; builtUtc = (Get-Date).ToUniversalTime().ToString("o") } |
    ConvertTo-Json | Set-Content -Path (Join-Path $downloadsDir "version.json") -Encoding UTF8

# ------------------------------------------------------------------ deploy (optional)
if ($Deploy) {
    $settingsFile = Join-Path $repo "site1.PublishSettings"
    if (-not (Test-Path $settingsFile)) { throw "site1.PublishSettings not found at the repo root - can't deploy." }
    Write-Host "Deploying the web app to the live site. This publishes the web app exactly as it is in this working tree,"
    Write-Host "including any EF migrations not yet applied there (they run against the live database on startup)."

    $publishXml = [xml](Get-Content $settingsFile)
    $webDeploy = $publishXml.publishData.publishProfile | Where-Object { $_.profileName -eq "darkmaster-002-site1 - Web Deploy" }
    # Never "$pwd" - that's PowerShell's current-directory variable and gets silently overwritten.
    $deployPassword = $webDeploy.userPWD
    Push-Location (Join-Path $repo "Mercurius")
    try {
        dotnet publish -c Release -p:PublishProfile="darkmaster-002-site1 - Web Deploy" -p:Password=$deployPassword -p:AllowUntrustedCertificate=true -v minimal
        if ($LASTEXITCODE -ne 0) { throw "Web deploy failed (exit $LASTEXITCODE)." }
    }
    finally {
        Pop-Location
        $deployPassword = $null
    }

    $health = Invoke-WebRequest "http://darkmaster-002-site1.dtempurl.com/health" -UseBasicParsing -TimeoutSec 120
    Write-Host "Live /health: $($health.Content)"
}

Write-Host "Done: Mercurius POS $display (build $build)."
