using System;
using System.IO;
using System.Text.Json;
using Mercurius.Shared.Admin;
using Microsoft.AspNetCore.Hosting;

namespace Mercurius.Services
{
    /// <summary>
    /// The downloadable builds of the POS app (Mercurius.Mobile): a Windows zip and an Android APK,
    /// produced by Mercurius.Mobile/publish-downloads.ps1 into Mercurius/Downloads — git-ignored
    /// (too big for GitHub, and this repo is public) and copied into the publish output by
    /// Mercurius.csproj, so they go live with a normal deploy.
    /// </summary>
    public static class MobileAppDownloads
    {
        public const string WindowsZipFileName = "Mercurius-POS-Windows-x64.zip";
        public const string AndroidApkFileName = "Mercurius-POS-Android.apk";
        public const string VersionFileName = "version.json";

        public static string Directory(IWebHostEnvironment env) => Path.Combine(env.ContentRootPath, "Downloads");

        public static MobileAppDownloadsDto Describe(IWebHostEnvironment env)
        {
            var directory = Directory(env);
            var info = new MobileAppDownloadsDto
            {
                Windows = DescribeFile(new FileInfo(Path.Combine(directory, WindowsZipFileName))),
                Android = DescribeFile(new FileInfo(Path.Combine(directory, AndroidApkFileName)))
            };

            // Written by the publish script; optional — the page still works without it.
            var versionFile = Path.Combine(directory, VersionFileName);
            if (File.Exists(versionFile))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(versionFile));
                    info.Version = doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() : null;
                    info.Build = doc.RootElement.TryGetProperty("build", out var b) && b.TryGetInt32(out var n) ? n : null;
                }
                catch (JsonException)
                {
                    // A malformed version file shouldn't take the download page down.
                }
            }
            return info;
        }

        private static DownloadFileDto DescribeFile(FileInfo file) => file.Exists
            ? new DownloadFileDto { Available = true, SizeMb = Math.Round(file.Length / 1024d / 1024d, 1), BuiltUtc = file.LastWriteTimeUtc }
            : new DownloadFileDto();
    }
}
