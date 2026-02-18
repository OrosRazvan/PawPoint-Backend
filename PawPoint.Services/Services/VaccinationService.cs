using Microsoft.EntityFrameworkCore;
using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Services
{
    public sealed class VaccinationService : IVaccinationService
    {
        private readonly Context _db;

        public VaccinationService(Context db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<VaccinationResponse>> GetAllForUserAsync(int userId)
        {
            var list = await _db.Vaccinations
                .AsNoTracking()
                .Include(v => v.Animal)
                .Include(v => v.VetCabinet)
                .Include(v => v.VetTimeSlot)
                .Where(v => v.Animal.UserId == userId)
                .OrderBy(v => v.NextDate ?? DateTime.MaxValue)
                .ThenBy(v => v.Animal.Name)
                .ThenBy(v => v.VaccineName)
                .ToListAsync();

            return list.Select(Map).ToList();
        }

        public async Task<IReadOnlyList<VaccinationResponse>> GetAllForAnimalAsync(int userId, int animalId)
        {
            var list = await _db.Vaccinations
                .AsNoTracking()
                .Include(v => v.Animal)
                .Include(v => v.VetCabinet)
                .Include(v => v.VetTimeSlot)
                .Where(v => v.Animal.UserId == userId && v.AnimalId == animalId)
                .OrderBy(v => v.NextDate ?? DateTime.MaxValue)
                .ThenBy(v => v.VaccineName)
                .ToListAsync();

            return list.Select(Map).ToList();
        }

        public async Task<VaccinationResponse> GetByIdAsync(int userId, int vaccinationId)
        {
            var vax = await _db.Vaccinations
                .AsNoTracking()
                .Include(v => v.Animal)
                .Include(v => v.VetCabinet)
                .Include(v => v.VetTimeSlot)
                .FirstOrDefaultAsync(v => v.Id == vaccinationId && v.Animal.UserId == userId);

            if (vax is null) throw new KeyNotFoundException("Vaccination not found.");
            return Map(vax);
        }

        public async Task<VaccinationResponse> CreateAsync(int userId, CreateVaccinationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.VaccineName))
                throw new ArgumentException("VaccineName is required.");

            if (request.LastDate.HasValue && request.NextDate.HasValue &&
                request.NextDate.Value < request.LastDate.Value)
                throw new ArgumentException("NextDateUtc must be >= LastDateUtc.");

            // Animal must belong to user
            var animal = await _db.Animals
                .FirstOrDefaultAsync(a => a.Id == request.AnimalId && a.UserId == userId);

            if (animal is null) throw new KeyNotFoundException("Animal not found for current user.");

            // Cabinet exists
            var cabinet = await _db.VetCabinets
                .FirstOrDefaultAsync(c => c.Id == request.VetCabinetId);

            if (cabinet is null) throw new KeyNotFoundException("Vet cabinet not found.");

            // Slot exists + belongs to cabinet + has capacity
            // Tracking because we update BookedCount
            var slot = await _db.VetTimeSlots
                .FirstOrDefaultAsync(s => s.Id == request.VetTimeSlotId);

            if (slot is null) throw new KeyNotFoundException("Time slot not found.");

            if (slot.VetCabinetId != request.VetCabinetId)
                throw new ArgumentException("Selected slot does not belong to selected cabinet.");

            // capacity check (în loc de IsBooked)
            if (slot.BookedCount >= slot.Capacity)
                throw new InvalidOperationException("Selected slot is full.");

            // optional: only future
            if (slot.StartTimeUtc <= DateTime.UtcNow)
                throw new InvalidOperationException("You can only book future slots.");

            // book it (like appointment)
            slot.BookedCount += 1;

            var vaccination = new Vaccination
            {
                AnimalId = animal.Id,
                VaccineName = request.VaccineName.Trim(),
                VetCabinetId = cabinet.Id,
                VetTimeSlotId = slot.Id,
                LastDate = request.LastDate,
                NextDate = request.NextDate,
                Notes = request.Notes?.Trim()
            };

            _db.Vaccinations.Add(vaccination);
            await _db.SaveChangesAsync();

            var created = await _db.Vaccinations
                .AsNoTracking()
                .Include(v => v.Animal)
                .Include(v => v.VetCabinet)
                .Include(v => v.VetTimeSlot)
                .FirstAsync(v => v.Id == vaccination.Id);

            return Map(created);
        }

        public async Task<VaccinationResponse> UpdateAsync(int userId, int vaccinationId, UpdateVaccinationRequest request)
        {
            var vax = await _db.Vaccinations
                .Include(v => v.Animal)
                .Include(v => v.VetCabinet)
                .Include(v => v.VetTimeSlot)
                .FirstOrDefaultAsync(v => v.Id == vaccinationId);

            if (vax is null) throw new KeyNotFoundException("Vaccination not found.");
            if (vax.Animal.UserId != userId) throw new UnauthorizedAccessException("Not allowed.");

            if (request.VaccineName is not null)
            {
                if (string.IsNullOrWhiteSpace(request.VaccineName))
                    throw new ArgumentException("VaccineName cannot be empty.");
                vax.VaccineName = request.VaccineName.Trim();
            }

            vax.LastDate = request.LastDate;
            vax.NextDate = request.NextDate;
            vax.Notes = request.Notes?.Trim();

            if (vax.LastDate.HasValue && vax.NextDate.HasValue &&
                vax.NextDate.Value < vax.LastDate.Value)
                throw new ArgumentException("NextDateUtc must be >= LastDateUtc.");

            await _db.SaveChangesAsync();

            var updated = await _db.Vaccinations
                .AsNoTracking()
                .Include(v => v.Animal)
                .Include(v => v.VetCabinet)
                .Include(v => v.VetTimeSlot)
                .FirstAsync(v => v.Id == vax.Id);

            return Map(updated);
        }

        public async Task DeleteAsync(int userId, int vaccinationId)
        {
            var vax = await _db.Vaccinations
                .Include(v => v.Animal)
                .Include(v => v.VetTimeSlot)
                .FirstOrDefaultAsync(v => v.Id == vaccinationId);

            if (vax is null) return;
            if (vax.Animal.UserId != userId) throw new UnauthorizedAccessException("Not allowed.");

            if (vax.VetTimeSlot.BookedCount > 0)
                vax.VetTimeSlot.BookedCount -= 1;

            _db.Vaccinations.Remove(vax);
            await _db.SaveChangesAsync();
        }

        private static VaccinationResponse Map(Vaccination v)
            => new(
                v.Id,
                v.AnimalId,
                v.Animal.Name,
                v.VaccineName,
                v.LastDate,
                v.NextDate,
                v.VetCabinetId,
                v.VetCabinet.Name,
                v.VetTimeSlotId,
                v.VetTimeSlot.StartTimeUtc,
                v.VetTimeSlot.EndTimeUtc,
                v.Notes
            );
    }
}
