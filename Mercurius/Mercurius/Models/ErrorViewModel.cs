namespace Mercurius.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        /// <summary>
        /// Tail of the day's log file. Populated by HomeController.Error when running
        /// in Development so the page surfaces what the server actually saw.
        /// </summary>
        public string? LogExcerpt { get; set; }

        public bool ShowLogExcerpt => !string.IsNullOrWhiteSpace(LogExcerpt);
    }
}