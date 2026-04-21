using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;

namespace PawPoint.ApiServices.Controllers
{
    [Authorize, Route("[controller]")]
    public class AnimalController(IAnimalService animalService, IIdentityService identityService)
        : BaseApiController(identityService)
    {
        private readonly IAnimalService _animalService = animalService;

        // POST /Animal/create
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromForm] CreateAnimalRequest request)
        {
            var userId = GetUserIdFromToken();
            var created = await _animalService.CreateAsync(userId, request);
            return Ok(created);
        }

        // GET /Animal/all
        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var userId = GetUserIdFromToken();
            var animals = await _animalService.GetMyAnimalsAsync(userId);
            return Ok(animals);
        }

        // GET /Animal/{animalId}
        [HttpGet("{animalId:int}")]
        public async Task<IActionResult> GetById(int animalId)
        {
            var userId = GetUserIdFromToken();
            var animal = await _animalService.GetByIdAsync(animalId, userId);
            if (animal is null) return NotFound();
            return Ok(animal);
        }

        // PUT /Animal/update/{animalId}
        [HttpPut("update/{animalId:int}")]
        public async Task<IActionResult> Update(int animalId, [FromForm] UpdateAnimalRequest request)
        {
            var userId = GetUserIdFromToken();
            var updated = await _animalService.UpdateAsync(animalId, userId, request);
            return Ok(updated);
        }

        // DELETE /Animal/delete/{animalId}
        [HttpDelete("delete/{animalId:int}")]
        public async Task<IActionResult> Delete(int animalId)
        {
            var userId = GetUserIdFromToken();
            await _animalService.DeleteAsync(animalId, userId);
            return Ok();
        }
    }
}
