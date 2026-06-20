using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Controllers
{
    [Authorize]
    public class PrescriptionsController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;

        public PrescriptionsController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
        }

        [Authorize(Policy = Common.ModuleRegistry.Pages.PRESCRIPTIONS_LIST)]
        public async Task<IActionResult> Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var prescriptions = await _unitOfWork.Repository<Prescription>().GetAllAsync(ct);
            // Hydrate doctors and patients
            var doctorIds = prescriptions.Select(p => p.DoctorId).Distinct().ToList();
            var patientIds = prescriptions.Select(p => p.PatientId).Distinct().ToList();
            var doctors = await _unitOfWork.Repository<Doctor>().FindAsync(d => doctorIds.Contains(d.Id), ct);
            var patients = await _unitOfWork.Repository<Customer>().FindAsync(c => patientIds.Contains(c.Id), ct);
            ViewBag.Doctors = doctors.ToDictionary(d => d.Id, d => $"{d.LastName}, {d.FirstName}");
            ViewBag.Patients = patients.ToDictionary(p => p.Id, p => $"{p.LastName}, {p.FirstName}");
            return View(prescriptions.OrderByDescending(p => p.PrescriptionDate));
        }

        [Authorize(Policy = Common.ModuleRegistry.Pages.PRESCRIPTIONS_CREATE)]
        public async Task<IActionResult> Create(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            await LoadViewData(ct);
            return View();
        }

        [HttpPost]
        [Authorize(Policy = Common.ModuleRegistry.Pages.PRESCRIPTIONS_CREATE)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Prescription prescription, int[] productIds, string[] dosages, decimal[] quantities, int[] durationDays, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (!ModelState.IsValid)
            {
                await LoadViewData(ct);
                return View(prescription);
            }

            prescription.PrescriptionNumber = $"RX-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
            prescription.PrescriptionDate = DateTime.UtcNow;
            prescription.Status = "Active";
            prescription.CreatedDate = DateTime.UtcNow;

            // Check if any product is controlled (S2/S3)
            if (productIds.Length > 0)
            {
                var products = await _unitOfWork.Repository<Product>().GetAllAsync(ct);
                var prodDict = products.ToDictionary(p => p.Id);
                prescription.IsControlled = productIds.Any(pid =>
                {
                    // Check via custom fields
                    return false; // Simplified – controlled check via MedicineBatch in Phase 3
                });
            }

            await _unitOfWork.Repository<Prescription>().AddAsync(prescription, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Add prescription items
            for (int i = 0; i < productIds.Length; i++)
            {
                if (productIds[i] > 0 && quantities[i] > 0)
                {
                    await _unitOfWork.Repository<PrescriptionItem>().AddAsync(new PrescriptionItem
                    {
                        PrescriptionId = prescription.Id,
                        ProductId = productIds[i],
                        Dosage = dosages.Length > i ? dosages[i] : "",
                        QuantityPrescribed = quantities[i],
                        DurationDays = durationDays.Length > i && durationDays[i] > 0 ? durationDays[i] : null
                    }, ct);
                }
            }
            await _unitOfWork.SaveChangesAsync(ct);

            TempData["Message"] = $"Prescription {prescription.PrescriptionNumber} created.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = Common.ModuleRegistry.Pages.PRESCRIPTIONS_EDIT)]
        public async Task<IActionResult> Edit(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null) return NotFound();
            var prescription = await _unitOfWork.Repository<Prescription>().GetByIdAsync(id.Value, ct);
            if (prescription == null) return NotFound();

            var items = await _unitOfWork.Repository<PrescriptionItem>()
                .FindAsync(pi => pi.PrescriptionId == prescription.Id, ct);
            ViewBag.Items = items.ToList();

            var products = await _unitOfWork.Repository<Product>().GetAllAsync(ct);
            ViewBag.Products = products.ToDictionary(p => p.Id, p => p.Name);

            await LoadViewData(ct);
            return View(prescription);
        }

        private async Task LoadViewData(CancellationToken ct = default)
        {
            var doctors = await _unitOfWork.Repository<Doctor>().GetAllAsync(ct);
            ViewBag.DoctorId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                doctors.Where(d => d.IsActive).OrderBy(d => d.LastName), "Id", "LastName");
            // Format: "LastName, FirstName"
            ViewBag.DoctorList = doctors.Where(d => d.IsActive).OrderBy(d => d.LastName).ToList();

            var patients = await _unitOfWork.Repository<Customer>().GetAllAsync(ct);
            ViewBag.PatientId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                patients.Where(c => c.IsActive).OrderBy(c => c.LastName), "Id", "LastName");
            ViewBag.PatientList = patients.Where(c => c.IsActive).OrderBy(c => c.LastName).ToList();

            var products = await _unitOfWork.Repository<Product>().GetAllAsync(ct);
            ViewBag.ProductList = products.Where(p => p.IsActive && p.CurrentStock > 0).OrderBy(p => p.Name).ToList();
        }
    }
}
