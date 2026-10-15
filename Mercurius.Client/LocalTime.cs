namespace Mercurius.Client
{
    // The server stores timestamps in UTC (DateTime.UtcNow) but they arrive here without a kind,
    // so they must be marked UTC before converting to the viewer's own time zone — the same
    // conversion the classic pages do in the browser with their <time class="local-time"> tags.
    public static class LocalTime
    {
        public static DateTime FromUtc(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();

        public static string FormatDate(DateTime utc) => FromUtc(utc).ToString("yyyy-MM-dd");
        public static string FormatDate(DateTime? utc) => utc.HasValue ? FormatDate(utc.Value) : "—";

        public static string FormatDateTime(DateTime utc) => FromUtc(utc).ToString("yyyy-MM-dd HH:mm");
        public static string FormatDateTime(DateTime? utc) => utc.HasValue ? FormatDateTime(utc.Value) : "—";
    }
}
