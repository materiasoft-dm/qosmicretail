using Mercurius.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Mercurius.ViewComponents
{
    public class SidebarViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke()
        {
            var isSuperAdmin = User.IsInRole("Administrator");
            var model = new SidebarViewModel
            {
                CanViewInvoiceList = isSuperAdmin || CheckAccessModule(Common.ModuleRegistry.Pages.INVOICE_INDEX),
                CanCreateInvoice = isSuperAdmin || CheckAccessModule(Common.ModuleRegistry.Pages.NEWSALE_CREATE),
                CanViewShipmentList = isSuperAdmin || CheckAccessModule(Common.ModuleRegistry.Pages.SHIPMENT_LIST),
                CanCreateShipment = isSuperAdmin || CheckAccessModule(Common.ModuleRegistry.Pages.SHIPMENT_CREATE),
                CanViewProductList = isSuperAdmin || CheckAccessModule(Common.ModuleRegistry.Pages.PRODUCTS_INDEX),
                CanViewCustomerList = isSuperAdmin || CheckAccessModule(Common.ModuleRegistry.Pages.CUSTOMER_LIST),
                CanAddCustomer = isSuperAdmin || CheckAccessModule(Common.ModuleRegistry.Pages.CUSTOMER_ADD),
                CanViewAdjustmentList = isSuperAdmin || CheckAccessModule(Common.ModuleRegistry.Pages.ADJUSTMENTS_INDEX),
                CanCreateAdjustments = isSuperAdmin || CheckAccessModule(Common.ModuleRegistry.Pages.ADJUSTMENTS_CREATE),
                CanViewReport = isSuperAdmin || CheckAccessModule(Common.ModuleRegistry.Pages.REPORT_INVENTORY) || 
                               CheckAccessModule(Common.ModuleRegistry.Pages.REPORT_SALES) ||
                               CheckAccessModule(Common.ModuleRegistry.Pages.REPORT_SHIPMENT) ||
                               CheckAccessModule(Common.ModuleRegistry.Pages.REPORT_REFUND) ||
                               CheckAccessModule(Common.ModuleRegistry.Pages.REPORT_ADJUSTMENTS),
                CanViewProductCategories = isSuperAdmin || CheckAccessModule(Common.ModuleRegistry.Pages.CONFIG_PRODUCT_CATEGORIES),
                CanViewLocations = isSuperAdmin || CheckAccessModule(Common.ModuleRegistry.Pages.CONFIG_LOCATIONS),
                CanViewColors = isSuperAdmin || CheckAccessModule(Common.ModuleRegistry.Pages.CONFIG_COLORS),
                CanViewSizes = isSuperAdmin || CheckAccessModule(Common.ModuleRegistry.Pages.CONFIG_SIZES),
                CanViewSuppliers = isSuperAdmin || CheckAccessModule(Common.ModuleRegistry.Pages.CONFIG_SUPPLIERS),
                CanViewAdjustmentReasons = isSuperAdmin || CheckAccessModule(Common.ModuleRegistry.Pages.CONFIG_ADJUSTMENT_REASONS),
                CanViewSettings = isSuperAdmin || CheckAccessModule(Common.ModuleRegistry.Pages.ADMIN_ROLES_MANAGEMENT) ||
                                  CheckAccessModule(Common.ModuleRegistry.Pages.ADMIN_USERS_MANAGEMENT) ||
                                  CheckAccessModule(Common.ModuleRegistry.Pages.CONFIG_PRODUCT_CATEGORIES) ||
                                  CheckAccessModule(Common.ModuleRegistry.Pages.CONFIG_LOCATIONS) ||
                                  CheckAccessModule(Common.ModuleRegistry.Pages.CONFIG_COLORS) ||
                                  CheckAccessModule(Common.ModuleRegistry.Pages.CONFIG_SIZES) ||
                                  CheckAccessModule(Common.ModuleRegistry.Pages.CONFIG_SUPPLIERS) ||
                                  CheckAccessModule(Common.ModuleRegistry.Pages.CONFIG_ADJUSTMENT_REASONS),
                CanViewUserList = isSuperAdmin || CheckAccessModule(Common.ModuleRegistry.Pages.ADMIN_USERS_MANAGEMENT)
            };

            return View(model);
        }

        private bool CheckAccessModule(string module)
        {
            return (User as ClaimsPrincipal)?.HasClaim(MercuriusClaimTypes.AccessPages, module) ?? false;
        }
    }

    public class SidebarViewModel
    {
        public bool CanViewInvoiceList { get; set; }
        public bool CanCreateInvoice { get; set; }
        public bool CanViewShipmentList { get; set; }
        public bool CanCreateShipment { get; set; }
        public bool CanViewProductList { get; set; }
        public bool CanViewCustomerList { get; set; }
        public bool CanAddCustomer { get; set; }
        public bool CanViewAdjustmentList { get; set; }
        public bool CanCreateAdjustments { get; set; }
        public bool CanViewReport { get; set; }
        public bool CanViewSettings { get; set; }
        public bool CanViewUserList { get; set; }
        public bool CanViewProductCategories { get; set; }
        public bool CanViewLocations { get; set; }
        public bool CanViewColors { get; set; }
        public bool CanViewSizes { get; set; }
        public bool CanViewSuppliers { get; set; }
        public bool CanViewAdjustmentReasons { get; set; }
    }
}
