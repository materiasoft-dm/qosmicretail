using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Mercurius.Common.Constants.MercuriusEnums;

namespace Mercurius.Common.Constants
{
    public class DefaultValues
    {
        public const int ShipmentStatusDefaultValue = (int)ShipmentStatusCollection.Draft;
        public const DayOfWeek SystemStartOfWeek = DayOfWeek.Sunday;
        public const string DefaultLocationName = "Branch1";
        public const string ImportStatusPending = "Pending";
    }

    /// <summary>
    /// Pagination constants used across all controllers for consistent page sizes.
    /// </summary>
    public static class PaginationDefaults
    {
        // Modal/Quick View page sizes (smaller for UI performance)
        public const int ModalPageSize = 10;
        public const int QuickSelectPageSize = 10;
        
        // Standard list page sizes
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 50;
        public const int LargeListPageSize = 25;
        
        // API page sizes
        public const int ApiDefaultPageSize = 20;
        public const int ApiMaxPageSize = 100;
        
        // Search/autocomplete (typically smaller)
        public const int SearchResultsPageSize = 15;
        public const int AutocompleteMaxResults = 10;
        
        // Export/Report page sizes
        public const int ExportPageSize = 100;
        public const int ReportPageSize = 50;
        
        // Validation bounds
        public const int MinPageSize = 1;
        public const int MaxAllowedPageSize = 100;
    }
}