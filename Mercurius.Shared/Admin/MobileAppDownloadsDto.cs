namespace Mercurius.Shared.Admin
{
    // What the "Download Mobile App" page shows. The files themselves are served by
    // /Downloads/Windows and /Downloads/Android on the server.
    public class MobileAppDownloadsDto
    {
        public string? Version { get; set; }
        public int? Build { get; set; }
        public DownloadFileDto Windows { get; set; } = new();
        public DownloadFileDto Android { get; set; } = new();
    }

    public class DownloadFileDto
    {
        public bool Available { get; set; }
        public double SizeMb { get; set; }
        public DateTime? BuiltUtc { get; set; }
    }
}
