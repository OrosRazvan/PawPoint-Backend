using Microsoft.EntityFrameworkCore;
using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Services
{
    public sealed class DewormingService(Context db) : IDewormingService
    {
        private readonly Context _db = db;

        public async Task<IReadOnlyList<DewormingResponse>> GetAllForUserAsync(int userId)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));

            var list = await _db.Dewormings
                .AsNoTracking()
                .Include(d => d.Animal)
                .Include(d => d.VetCabinet)
                .Include(d => d.VetTimeSlot)
                .Where(d => d.Animal.UserId == userId)
                .OrderBy(d => d.NextDate ?? DateTime.MaxValue)
                .ThenBy(d => d.Animal.Name)
                .ToListAsync();

            return list.Select(Map).ToList();
        }

        public async Task<IReadOnlyList<DewormingResponse>> GetAllForAnimalAsync(int userId, int animalId)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (animalId <= 0) throw new ArgumentOutOfRangeException(nameof(animalId));

            var list = await _db.Dewormings
                .AsNoTracking()
                .Include(d => d.Animal)
                .Include(d => d.VetCabinet)
                .Include(d => d.VetTimeSlot)
                .Where(d => d.Animal.UserId == userId && d.AnimalId == animalId)
                .OrderBy(d => d.NextDate ?? DateTime.MaxValue)
                .ToListAsync();

            return list.Select(Map).ToList();
        }

        public async Task<DewormingResponse> GetByIdAsync(int userId, int dewormingId)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (dewormingId <= 0) throw new ArgumentOutOfRangeException(nameof(dewormingId));

            var entity = await _db.Dewormings
                .AsNoTracking()
                .Include(d => d.Animal)
                .Include(d => d.VetCabinet)
                .Include(d => d.VetTimeSlot)
                .FirstOrDefaultAsync(d => d.Id == dewormingId && d.Animal.UserId == userId);

            if (entity is null)
                throw new KeyNotFoundException("Deworming not found.");

            return Map(entity);
        }

        public async Task<DewormingResponse> CreateAsync(int userId, CreateDewormingRequest request)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (request is null) throw new ArgumentNullException(nameof(request));

            if (request.AnimalId <= 0) throw new ArgumentException("AnimalId is required.");
            if (request.VetCabinetId <= 0) throw new ArgumentException("VetCabinetId is required.");
            if (request.VetTimeSlotId <= 0) throw new ArgumentException("VetTimeSlotId is required.");

            if (request.IntervalDays <= 0)
                throw new ArgumentException("IntervalDays must be > 0.");

            // Animal belongs to user
            var animal = await _db.Animals
                .FirstOrDefaultAsync(a => a.Id == request.AnimalId && a.UserId == userId);

            if (animal is null)
                throw new KeyNotFoundException("Animal not found for current user.");

            // Cabinet exists
            var cabinet = await _db.VetCabinets
                .FirstOrDefaultAsync(c => c.Id == request.VetCabinetId);

            if (cabinet is null)
                throw new KeyNotFoundException("Vet cabinet not found.");

            // Slot exists (tracking, because we update BookedCount)
            var slot = await _db.VetTimeSlots
                .FirstOrDefaultAsync(s => s.Id == request.VetTimeSlotId);

            if (slot is null)
                throw new KeyNotFoundException("Time slot not found.");

            if (slot.VetCabinetId != request.VetCabinetId)
                throw new ArgumentException("Selected slot does not belong to selected cabinet.");

            // Capacity check
            if (slot.BookedCount >= slot.Capacity)
                throw new InvalidOperationException("Selected slot is full.");

            //// Only future slots
            //if (slot.StartTimeUtc <= DateTime.UtcNow)
            //    throw new InvalidOperationException("You can only book future slots.");

            // Book slot capacity
            slot.BookedCount += 1;

            var dateUtc = slot.StartTimeUtc;
            var nextDateUtc = dateUtc.AddDays(request.IntervalDays);

            var entity = new Deworming
            {
                AnimalId = animal.Id,
                Type = request.Type,
                VetCabinetId = cabinet.Id,
                VetTimeSlotId = slot.Id,
                Date = dateUtc,
                IntervalDays = request.IntervalDays,
                NextDate = nextDateUtc,
                Notes = request.Notes?.Trim()
            };

            _db.Dewormings.Add(entity);
            await _db.SaveChangesAsync();

            var created = await _db.Dewormings
                .AsNoTracking()
                .Include(d => d.Animal)
                .Include(d => d.VetCabinet)
                .Include(d => d.VetTimeSlot)
                .FirstAsync(d => d.Id == entity.Id);

            return Map(created);
        }

        public async Task<DewormingResponse> UpdateAsync(int userId, int dewormingId, UpdateDewormingRequest request)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (dewormingId <= 0) throw new ArgumentOutOfRangeException(nameof(dewormingId));
            if (request is null) throw new ArgumentNullException(nameof(request));

            var entity = await _db.Dewormings
                .Include(d => d.Animal)
                .Include(d => d.VetCabinet)
                .Include(d => d.VetTimeSlot)
                .FirstOrDefaultAsync(d => d.Id == dewormingId);

            if (entity is null)
                throw new KeyNotFoundException("Deworming not found.");

            if (entity.Animal.UserId != userId)
                throw new UnauthorizedAccessException("Not allowed.");

            if (request.Type.HasValue)
                entity.Type = request.Type.Value;

            if (request.IntervalDays.HasValue)
            {
                if (request.IntervalDays.Value <= 0)
                    throw new ArgumentException("IntervalDays must be > 0.");

                entity.IntervalDays = request.IntervalDays.Value;
                entity.NextDate = entity.Date.AddDays(entity.IntervalDays);
            }

            entity.Notes = request.Notes?.Trim();

            await _db.SaveChangesAsync();

            var updated = await _db.Dewormings
                .AsNoTracking()
                .Include(d => d.Animal)
                .Include(d => d.VetCabinet)
                .Include(d => d.VetTimeSlot)
                .FirstAsync(d => d.Id == entity.Id);

            return Map(updated);
        }

        public async Task DeleteAsync(int userId, int dewormingId)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (dewormingId <= 0) throw new ArgumentOutOfRangeException(nameof(dewormingId));

            var entity = await _db.Dewormings
                .Include(d => d.Animal)
                .Include(d => d.VetTimeSlot)
                .FirstOrDefaultAsync(d => d.Id == dewormingId);

            if (entity is null) return;

            if (entity.Animal.UserId != userId)
                throw new UnauthorizedAccessException("Not allowed.");

            // Release slot capacity
            if (entity.VetTimeSlot.BookedCount > 0)
                entity.VetTimeSlot.BookedCount -= 1;

            _db.Dewormings.Remove(entity);
            await _db.SaveChangesAsync();
        }

        private static DewormingResponse Map(Deworming d)
            => new(
                d.Id,
                d.AnimalId,
                d.Animal.Name,
                d.Type.ToString(),
                d.Date,
                d.IntervalDays,
                d.NextDate,
                d.VetCabinetId,
                d.VetCabinet.Name,
                d.VetTimeSlotId,
                d.VetTimeSlot.StartTimeUtc,
                d.VetTimeSlot.EndTimeUtc,
                d.Notes
            );
    }
}
