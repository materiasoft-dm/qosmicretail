namespace Mercurius.Models;

public class LogFileInfo
{
    public string FileName { get; set; } = "";
    public DateTime CreatedDate { get; set; }
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string FormattedSize => FormatBytes(Size);
    private static string FormatBytes(long b) { string[] s = {"B","KB","MB","GB"}; int o=0; double x=b; while(x>=1024&&o<s.Length-1){o++;x/=1024;} return $"{x:0.##} {s[o]}"; }
}
