using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;

namespace PawPoint.ApiServices.Controllers
{
    [Authorize, Route("[controller]")]
    public class ServicePriceController : ControllerBase
    {
        private readonly IServicePriceService _servicePriceService;

        public ServicePriceController(IServicePriceService servicePriceService)
        {
            _servicePriceService = servicePriceService;
        }

        [HttpGet("price")]
        public async Task<IActionResult> GetPrice([FromQuery] ServicePriceRequest request)
        {
            var result = await _servicePriceService.GetPriceAsync(request);

            if (result is null)
                return NotFound();

            return Ok(result);
        }
    }
}