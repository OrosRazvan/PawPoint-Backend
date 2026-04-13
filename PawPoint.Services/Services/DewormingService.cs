using Microsoft.EntityFrameworkCore;
using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.DB.Enums;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Services
{
    public sealed class DewormingService(Context db, INotificationService notificationService) : IDewormingService
    {
        private readonly Context _db = db;
        private readonly INotificationService _notificationService = notificationService;

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

            var animal = await _db.Animals
                .FirstOrDefaultAsync(a => a.Id == request.AnimalId && a.UserId == userId);

            if (animal is null)
                throw new KeyNotFoundException("Animal not found for current user.");

            var cabinet = await _db.VetCabinets
                .FirstOrDefaultAsync(c => c.Id == request.VetCabinetId);

            if (cabinet is null)
                throw new KeyNotFoundException("Vet cabinet not found.");

            var slot = await _db.VetTimeSlots
                .FirstOrDefaultAsync(s => s.Id == request.VetTimeSlotId);

            if (slot is null)
                throw new KeyNotFoundException("Time slot not found.");

            if (slot.VetCabinetId != request.VetCabinetId)
                throw new ArgumentException("Selected slot does not belong to selected cabinet.");

            if (slot.BookedCount >= slot.Capacity)
                throw new InvalidOperationException("Selected slot is full.");

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

            await _notificationService.CreateNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.DewormingBooked,
                    userId,
                    "Deworming booked",
                    $"{animal.Name} has been scheduled for deworming ({entity.Type}) on {entity.Date:dd.MM.yyyy}. DEW:{entity.Id}"
                )
            );

            await ScheduleDewormingRemindersAsync(
                userId,
                animal.Name,
                entity.Type.ToString(),
                entity.Id,
                entity.NextDate
            );

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

            await _notificationService.CreateNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.DewormingUpdated,
                    userId,
                    "Deworming updated",
                    $"{entity.Animal.Name}'s deworming ({entity.Type}) has been updated. DEW:{entity.Id}"
                )
            );

            await ScheduleDewormingRemindersAsync(
                userId,
                entity.Animal.Name,
                entity.Type.ToString(),
                entity.Id,
                entity.NextDate
            );

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

            await _notificationService.CreateNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.DewormingCancelled,
                    userId,
                    "Deworming removed",
                    $"{entity.Animal.Name}'s deworming ({entity.Type}) has been removed. DEW:{entity.Id}"
                )
            );

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

        private async Task ScheduleDewormingRemindersAsync(
            int userId,
            string animalName,
            string type,
            int dewormingId,
            DateTime? nextDateUtc)
        {
            if (!nextDateUtc.HasValue)
                return;

            await _notificationService.ScheduleNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.DewormingReminder,
                    userId,
                    "Deworming reminder",
                    $"{animalName} needs deworming ({type}) in 7 days, around {nextDateUtc.Value:dd.MM.yyyy}. DEW:{dewormingId}"
                ),
                nextDateUtc.Value.AddDays(-7)
            );

            await _notificationService.ScheduleNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.DewormingReminder,
                    userId,
                    "Deworming reminder",
                    $"{animalName} needs deworming ({type}) tomorrow, around {nextDateUtc.Value:dd.MM.yyyy}. DEW:{dewormingId}"
                ),
                nextDateUtc.Value.AddDays(-1)
            );
        }
    }
}