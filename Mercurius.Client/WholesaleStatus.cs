using MudBlazor;

namespace Mercurius.Client
{
    // A wholesale order's status, worded by what it means for payment (see WholesaleApiController):
    // StatusCollection.InvoiceStatus 2 Completed = paid, 6 DeferredPayment = unpaid, 7 PartiallyPaid.
    public static class WholesaleStatus
    {
        public static string Label(int statusId) => statusId switch
        {
            2 => "Paid",
            4 => "Refunded",
            5 => "Deleted",
            6 => "Unpaid",
            7 => "Partially paid",
            _ => "Unknown"
        };

        public static Color Color(int statusId) => statusId switch
        {
            2 => MudBlazor.Color.Success,
            4 => MudBlazor.Color.Error,
            5 => MudBlazor.Color.Dark,
            6 => MudBlazor.Color.Warning,
            7 => MudBlazor.Color.Info,
            _ => MudBlazor.Color.Default
        };

        // A due date is a calendar date, not a moment, so it's shown as stored (no UTC conversion).
        public static string FormatDueDate(DateTime? due) => due?.ToString("yyyy-MM-dd") ?? "—";

        public static bool IsOverdue(DateTime? due, decimal balance) => balance > 0 && due.HasValue && due.Value.Date < DateTime.Today;
    }
}
