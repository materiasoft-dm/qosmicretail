namespace Mercurius.Common.Helpers
{
    public static class DateTimeExtensions
    {
        public static DateTime StartOfWeek(this DateTime dt)
        {
            int diff = (7 + (dt.DayOfWeek - Constants.DefaultValues.SystemStartOfWeek)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }

        public static DateTime LastDayOfWeek(this DateTime date)
        {
            DateTime ldowDate = StartOfWeek(date).AddDays(6);
            return ldowDate;
        }
    }
}
