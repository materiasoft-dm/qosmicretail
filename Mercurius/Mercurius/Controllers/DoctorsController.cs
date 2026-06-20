using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Controllers
{
    [Authorize]
    public class DoctorsController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;

        public DoctorsController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
        }

        [Authorize(Policy = Common.ModuleRegistry.Pages.DOCTORS_LIST)]
        public async Task<IActionResult> Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var doctors = await _unitOfWork.Repository<Doctor>().GetAllAsync(ct);
            return View(doctors.OrderBy(d => d.LastName).ThenBy(d => d.FirstName));
        }

        [Authorize(Policy = Common.ModuleRegistry.Pages.DOCTORS_CREATE)]
        public IActionResult Create(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View();
        }

        [HttpPost]
        [Authorize(Policy = Common.ModuleRegistry.Pages.DOCTORS_CREATE)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Doctor doctor, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (ModelState.IsValid)
            {
                doctor.CreatedDate = DateTime.UtcNow;
                await _unitOfWork.Repository<Doctor>().AddAsync(doctor, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }
            return View(doctor);
        }

        [Authorize(Policy = Common.ModuleRegistry.Pages.DOCTORS_EDIT)]
        public async Task<IActionResult> Edit(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null) return NotFound();
            var doctor = await _unitOfWork.Repository<Doctor>().GetByIdAsync(id.Value, ct);
            if (doctor == null) return NotFound();
            return View(doctor);
        }

        [HttpPost]
        [Authorize(Policy = Common.ModuleRegistry.Pages.DOCTORS_EDIT)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Doctor doctor, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id != doctor.Id) return NotFound();
            if (ModelState.IsValid)
            {
                await _unitOfWork.Repository<Doctor>().UpdateAsync(doctor, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }
            return View(doctor);
        }

        [HttpPost]
        [Authorize(Policy = Common.ModuleRegistry.Pages.DOCTORS_DELETE)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var doctor = await _unitOfWork.Repository<Doctor>().GetByIdAsync(id, ct);
            if (doctor != null)
            {
                doctor.IsActive = false;
                await _unitOfWork.Repository<Doctor>().UpdateAsync(doctor, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
