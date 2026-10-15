using MudBlazor;

namespace Mercurius.Client
{
    // Display text and colours for PurchaseOrder.Status, matching the classic list's badges:
    // PendingApproval -> Approved -> OrderSent -> ReceivedComplete | ReceivedIncomplete.
    public static class PurchaseOrderStatus
    {
        public const string PendingApproval = "PendingApproval";
        public const string Approved = "Approved";
        public const string OrderSent = "OrderSent";

        // The shipment form only offers approved or sent orders (ShipmentsApiController.GetPurchaseOrders).
        public static bool CanReceive(string status) => status is Approved or OrderSent;

        public static string Label(string status) => status switch
        {
            "PendingApproval" => "Pending approval",
            "Approved" => "Approved",
            "OrderSent" => "Sent to supplier",
            "ReceivedComplete" => "Received",
            "ReceivedIncomplete" => "Received (incomplete)",
            _ => status
        };

        public static Color Color(string status) => status switch
        {
            "PendingApproval" => MudBlazor.Color.Warning,
            "Approved" => MudBlazor.Color.Info,
            "OrderSent" => MudBlazor.Color.Primary,
            "ReceivedComplete" => MudBlazor.Color.Success,
            "ReceivedIncomplete" => MudBlazor.Color.Error,
            _ => MudBlazor.Color.Default
        };
    }
}
