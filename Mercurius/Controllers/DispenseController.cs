using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Controllers
{
    [Authorize]
    public class DispenseController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;

        public DispenseController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>Show dispensing form for a prescription.</summary>
        public async Task<IActionResult> Index(int prescriptionId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var prescription = await _unitOfWork.Repository<Prescription>().GetByIdAsync(prescriptionId, ct);
            if (prescription == null) return NotFound();

            var items = await _unitOfWork.Repository<PrescriptionItem>()
                .FindAsync(pi => pi.PrescriptionId == prescriptionId, ct);

            // Hydrate
            var doctor = await _unitOfWork.Repository<Doctor>().GetByIdAsync(prescription.DoctorId, ct);
            var patient = await _unitOfWork.Repository<Customer>().GetByIdAsync(prescription.PatientId, ct);

            // Get available batches for each prescribed product (FEFO sorted)
            var productIds = items.Select(i => i.ProductId).Distinct().ToList();
            var batches = await _unitOfWork.Repository<MedicineBatch>()
                .FindAsync(b => productIds.Contains(b.ProductId) && b.IsActive && b.RemainingQuantity > 0, ct);

            var products = await _unitOfWork.Repository<Product>().GetAllAsync(ct);
            var prodDict = products.ToDictionary(p => p.Id);

            ViewBag.Prescription = prescription;
            ViewBag.Doctor = doctor;
            ViewBag.Patient = patient;
            ViewBag.Items = items.ToList();
            ViewBag.Batches = batches.OrderBy(b => b.ExpiryDate).ToList();
            ViewBag.Products = prodDict;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dispense(int prescriptionId, int[] itemIds, int[] batchIds, decimal[] quantities, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var prescription = await _unitOfWork.Repository<Prescription>().GetByIdAsync(prescriptionId, ct);
            if (prescription == null) return NotFound();

            for (int i = 0; i < itemIds.Length; i++)
            {
                if (itemIds[i] <= 0 || batchIds[i] <= 0 || quantities[i] <= 0) continue;

                // Deduct from batch
                var batch = await _unitOfWork.Repository<MedicineBatch>().GetByIdAsync(batchIds[i], ct);
                if (batch != null && batch.RemainingQuantity >= quantities[i])
                {
                    batch.RemainingQuantity -= quantities[i];
                    if (batch.RemainingQuantity <= 0) batch.IsActive = false;
                    await _unitOfWork.Repository<MedicineBatch>().UpdateAsync(batch, ct);
                }

                // Update prescription item
                var item = await _unitOfWork.Repository<PrescriptionItem>().GetByIdAsync(itemIds[i], ct);
                if (item != null)
                {
                    item.QuantityDispensed += quantities[i];
                    await _unitOfWork.Repository<PrescriptionItem>().UpdateAsync(item, ct);
                }

                // Deduct from product stock
                var itemRef = await _unitOfWork.Repository<PrescriptionItem>().GetByIdAsync(itemIds[i], ct);
                if (itemRef != null)
                {
                    var product = await _unitOfWork.Repository<Product>().GetByIdAsync(itemRef.ProductId, ct);
                    if (product != null)
                    {
                        product.CurrentStock -= quantities[i];
                        if (product.CurrentStock < 0) product.CurrentStock = 0;
                        await _unitOfWork.Repository<Product>().UpdateAsync(product, ct);
                    }
                }
            }

            // Check if fully dispensed
            var allItems = await _unitOfWork.Repository<PrescriptionItem>()
                .FindAsync(pi => pi.PrescriptionId == prescriptionId, ct);
            if (allItems.All(pi => pi.QuantityDispensed >= pi.QuantityPrescribed))
            {
                prescription.Status = "Dispensed";
            }
            else
            {
                prescription.Status = "Partially Dispensed";
            }
            prescription.ProcessedBy = Guid.NewGuid(); // placeholder – use actual user
            await _unitOfWork.Repository<Prescription>().UpdateAsync(prescription, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            TempData["Message"] = $"Dispensed items from prescription {prescription.PrescriptionNumber}.";
            return RedirectToAction("Index", "Prescriptions");
        }
    }
}
