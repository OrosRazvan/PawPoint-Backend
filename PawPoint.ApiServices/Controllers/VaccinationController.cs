using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;

namespace PawPoint.ApiServices.Controllers
{
    [Authorize, Route("[controller]")]
    public class VaccinationController : BaseApiController
    {
        private readonly IVaccinationService _vaccinationService;
        private readonly IAppointmentService _appointmentService;

        public VaccinationController(
            IVaccinationService vaccinationService,
            IAppointmentService appointmentService,
            IIdentityService identityService)
            : base(identityService)
        {
            _vaccinationService = vaccinationService;
            _appointmentService = appointmentService;
        }

        [HttpGet("vet-cabinets")]
        public async Task<IActionResult> GetVetCabinets(
            [FromQuery] string? serviceType,
            [FromQuery] string? sortBy)
        {
            var result = await _appointmentService.GetVetCabinetsAsync(serviceType, sortBy);
            return Ok(result);
        }

        [HttpGet("availability/{vetCabinetId:int}")]
        public async Task<IActionResult> GetAvailability(
            int vetCabinetId,
            [FromQuery] DateOnly from,
            [FromQuery] DateOnly to)
        {
            var result = await _appointmentService.GetAvailabilityAsync(vetCabinetId, from, to);
            return Ok(result);
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var userId = GetUserIdFromToken();
            var result = await _vaccinationService.GetAllForUserAsync(userId);
            return Ok(result);
        }

        [HttpGet("{vaccinationId:int}")]
        public async Task<IActionResult> GetById(int vaccinationId)
        {
            var userId = GetUserIdFromToken();
            var result = await _vaccinationService.GetByIdAsync(userId, vaccinationId);
            return Ok(result);
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateVaccinationRequest request)
        {
            var userId = GetUserIdFromToken();
            var created = await _vaccinationService.CreateAsync(userId, request);
            return Ok(created);
        }

        [HttpPut("update/{vaccinationId:int}")]
        public async Task<IActionResult> Update(
            int vaccinationId,
            [FromBody] UpdateVaccinationRequest request)
        {
            var userId = GetUserIdFromToken();
            var updated = await _vaccinationService.UpdateAsync(userId, vaccinationId, request);
            return Ok(updated);
        }

        [HttpDelete("delete/{vaccinationId:int}")]
        public async Task<IActionResult> Delete(int vaccinationId)
        {
            var userId = GetUserIdFromToken();
            await _vaccinationService.DeleteAsync(userId, vaccinationId);
            return Ok();
        }
    }
}
