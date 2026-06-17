using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mercurius.Common.Helpers
{
    public class DateTimeHelper
    {
        public static string DateTimeFormat = "MM/dd/yyyy h:mm tt";
        public static string DateFormat = "MM/dd/yyyy";
        public static string DateTimeFormatUrlParam = "MMddyyyyhhmmtt";
        public static string ShortDateFormat = "MM/dd/yy";
        public static string DateFormatWithMonthString = "MMMM dd, yyyy";

        // Default fallback if no timezone has been configured.
        public const string DefaultTimeZoneId = "Singapore Standard Time";

        // Set at startup from configuration ("Localization:TimeZoneId") or per-request from
        // the signed-in user's preferences. Until proper per-user/per-branch wiring lands,
        // the default keeps existing behaviour.
        public static string ConfiguredTimeZoneId { get; set; } = DefaultTimeZoneId;

        public static DateTime GetLocalizedDate()
        {
            return GetLocalizedDate(ConfiguredTimeZoneId);
        }

        public static DateTime GetLocalizedDate(string? timeZoneId)
        {
            var tzId = string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZoneId : timeZoneId;
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                return TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
            }
            catch (TimeZoneNotFoundException)
            {
                return DateTime.UtcNow;
            }
        }
    }
}
