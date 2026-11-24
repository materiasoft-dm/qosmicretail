using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Shipments;

namespace Mercurius.Services
{
    /// <summary>
    /// Putting a delivery on the shelf. For each product that arrived: the units are added to its
    /// stock, a batch is started at the delivery's cost and sale price (so FIFO pricing queues it
    /// behind older stock — see BatchPricingService), and the line is kept on the shipment. When the
    /// shipment is against a purchase order, each of its lines' received quantity goes up and the
    /// order becomes Received (every line in full) or Received (incomplete).
    ///
    /// Stock only ever went up through batches, adjustments, CSV imports and refund restocks before
    /// this: recording a shipment as received changed nothing on the shelf.
    /// </summary>
    public class StockReceiving
    {
        public const string PurchaseOrderReceivedComplete = "ReceivedComplete";
        public const string PurchaseOrderReceivedIncomplete = "ReceivedIncomplete";

        // A purchase order can be received against until it is fully received.
        public static readonly string[] ReceivablePurchaseOrderStatuses = { "Approved", "OrderSent", PurchaseOrderReceivedIncomplete };

        private readonly BatchPricingService _batchPricing;

        public StockReceiving(BatchPricingService batchPricing)
        {
            _batchPricing = batchPricing;
        }

        /// <summary>
        /// Why a delivery's lines can't be received, or null when they can. Checked before anything is
        /// written, so a bad line never leaves half a delivery behind.
        /// </summary>
        public static string? Validate(IReadOnlyList<ShipmentItemRequest> items)
        {
            if (items.Count == 0) return "Add at least one item that arrived.";
            foreach (var item in items)
            {
                if (item.ProductId <= 0) return "Every line needs a product.";
                if (item.Quantity <= 0) return "Every line needs a quantity above zero.";
                if (item.UnitCost < 0 || item.UnitSalePrice < 0) return "Prices can't be negative.";
                if (item.BatchNumber is { Length: > 100 }) return "A batch number can be at most 100 characters.";
            }
            return null;
        }

        /// <summary>
        /// Receives <paramref name="items"/> against the (already saved) <paramref name="shipment"/>. Returns an
        /// error message when a product isn't in the catalog, else null. The caller owns the transaction.
        /// </summary>
        public async Task<string?> ReceiveAsync(IUnitOfWork unitOfWork, ShipmentArrival shipment, PurchaseOrder? purchaseOrder,
            IReadOnlyList<ShipmentItemRequest> items, DateTime receivedUtc, CancellationToken ct = default)
        {
            var productIds = items.Select(i => i.ProductId).Distinct().ToList();
            var products = (await unitOfWork.Repository<Product>().FindAsync(p => productIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);
            if (productIds.Any(id => !products.ContainsKey(id)))
            {
                return "One of the products is no longer in the catalog.";
            }

            foreach (var item in items)
            {
                var product = products[item.ProductId];

                await unitOfWork.Repository<ShipmentArrivalItem>().AddAsync(new ShipmentArrivalItem
                {
                    ShipmentArrivalId = shipment.Id,
                    ProductId = product.Id,
                    ItemCount = item.Quantity,
                    CostPrice = item.UnitCost,
                    SalesPrice = item.UnitSalePrice
                }, ct);

                await unitOfWork.Repository<MedicineBatch>().AddAsync(new MedicineBatch
                {
                    ProductId = product.Id,
                    BatchNumber = string.IsNullOrWhiteSpace(item.BatchNumber) ? $"SHP-{shipment.Id}" : item.BatchNumber.Trim(),
                    ExpiryDate = item.ExpiryDate?.ToUniversalTime() ?? MedicineBatch.NoExpiry,
                    ReceivedDate = receivedUtc,
                    UnitCost = item.UnitCost,
                    UnitSalePrice = item.UnitSalePrice,
                    InitialQuantity = item.Quantity,
                    RemainingQuantity = item.Quantity,
                    IsActive = true
                }, ct);

                product.CurrentStock += item.Quantity;
                await unitOfWork.Repository<Product>().UpdateAsync(product, ct);
            }
            await unitOfWork.SaveChangesAsync(ct);

            // The displayed price follows the oldest batch that still has stock.
            foreach (var productId in productIds)
            {
                await _batchPricing.RefreshActivePriceAsync(unitOfWork, productId, ct);
            }

            if (purchaseOrder != null)
            {
                await UpdatePurchaseOrderAsync(unitOfWork, purchaseOrder, items, ct);
            }
            return null;
        }

        private static async Task UpdatePurchaseOrderAsync(IUnitOfWork unitOfWork, PurchaseOrder order,
            IReadOnlyList<ShipmentItemRequest> items, CancellationToken ct)
        {
            var arrivedByProduct = items.GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
            var lines = (await unitOfWork.Repository<PurchaseOrderItem>().FindAsync(l => l.PurchaseOrderId == order.Id, ct)).ToList();

            foreach (var line in lines)
            {
                if (!arrivedByProduct.TryGetValue(line.ProductId, out var arrived)) continue;
                line.ReceivedQuantity = (line.ReceivedQuantity ?? 0m) + arrived;
                // A product ordered on two lines: everything that arrived counts against the first.
                arrivedByProduct[line.ProductId] = 0m;
                await unitOfWork.Repository<PurchaseOrderItem>().UpdateAsync(line, ct);
            }

            var everyLineInFull = lines.All(l => (l.ReceivedQuantity ?? 0m) >= l.Quantity);
            order.Status = everyLineInFull ? PurchaseOrderReceivedComplete : PurchaseOrderReceivedIncomplete;
            order.UpdatedDate = DateTime.UtcNow;
            await unitOfWork.Repository<PurchaseOrder>().UpdateAsync(order, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }
}
