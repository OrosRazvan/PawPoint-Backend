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

            if (slot.StartTimeUtc <= DateTime.UtcNow)
                throw new InvalidOperationException("You can only book future slots.");

            var price = await _db.VetServicePrices
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.VetCabinetId == cabinet.Id &&
                    p.ServiceType == "Deworming" &&
                    p.DewormingType == request.Type);

            slot.BookedCount += 1;

            var dateUtc = slot.StartTimeUtc;
            var nextDateUtc = dateUtc.AddDays(GetDefaultIntervalDays(request.Type));

            var entity = new Deworming
            {
                AnimalId = animal.Id,
                Type = request.Type,
                VetCabinetId = cabinet.Id,
                VetTimeSlotId = slot.Id,
                Date = dateUtc,
                NextDate = nextDateUtc,
                Price = price?.Price,
                Currency = price?.Currency ?? Currency.Eur,
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

        public async Task<DewormingResponse> UpdateAsync(
    int userId,
    int dewormingId,
    UpdateDewormingRequest request)
        {
            if (userId <= 0)
                throw new ArgumentOutOfRangeException(nameof(userId));

            if (dewormingId <= 0)
                throw new ArgumentOutOfRangeException(nameof(dewormingId));

            if (request is null)
                throw new ArgumentNullException(nameof(request));

            var entity = await _db.Dewormings
                .Include(d => d.Animal)
                .Include(d => d.VetCabinet)
                .Include(d => d.VetTimeSlot)
                .FirstOrDefaultAsync(d => d.Id == dewormingId);

            if (entity is null)
                throw new KeyNotFoundException("Deworming not found.");

            if (entity.Animal.UserId != userId)
                throw new UnauthorizedAccessException("Not allowed.");

            if (request.AnimalId.HasValue && request.AnimalId.Value != entity.AnimalId)
            {
                var animal = await _db.Animals.FirstOrDefaultAsync(a =>
                    a.Id == request.AnimalId.Value &&
                    a.UserId == userId &&
                    !a.IsDeleted);

                if (animal is null)
                    throw new KeyNotFoundException("Animal not found for current user.");

                entity.AnimalId = animal.Id;
                entity.Animal = animal;
            }

            if (request.Type.HasValue)
                entity.Type = request.Type.Value;

            if (request.VetTimeSlotId.HasValue &&
                request.VetTimeSlotId.Value != entity.VetTimeSlotId)
            {
                var newSlot = await _db.VetTimeSlots
                    .Include(s => s.VetCabinet)
                    .FirstOrDefaultAsync(s => s.Id == request.VetTimeSlotId.Value);

                if (newSlot is null)
                    throw new KeyNotFoundException("Selected time slot not found.");

                if (request.VetCabinetId.HasValue &&
                    newSlot.VetCabinetId != request.VetCabinetId.Value)
                    throw new InvalidOperationException("Selected time slot does not belong to selected cabinet.");

                if (newSlot.StartTimeUtc <= DateTime.UtcNow)
                    throw new InvalidOperationException("You can only book future slots.");

                if (newSlot.BookedCount >= newSlot.Capacity)
                    throw new InvalidOperationException("Selected slot is full.");

                entity.VetTimeSlot.BookedCount =
                    Math.Max(0, entity.VetTimeSlot.BookedCount - 1);

                newSlot.BookedCount += 1;

                entity.VetTimeSlotId = newSlot.Id;
                entity.VetTimeSlot = newSlot;

                entity.VetCabinetId = newSlot.VetCabinetId;
                entity.VetCabinet = newSlot.VetCabinet;

                entity.Date = newSlot.StartTimeUtc;
            }
            else if (request.VetCabinetId.HasValue &&
                     request.VetCabinetId.Value != entity.VetCabinetId)
            {
                throw new InvalidOperationException("To change the cabinet, select a new time slot from that cabinet.");
            }

            entity.NextDate = entity.Date.AddDays(GetDefaultIntervalDays(entity.Type));

            var price = await _db.VetServicePrices
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.VetCabinetId == entity.VetCabinetId &&
                    p.ServiceType == "Deworming" &&
                    p.DewormingType == entity.Type);

            entity.Price = price?.Price;
            entity.Currency = price?.Currency ?? Currency.Eur;

            if (request.Notes is not null)
                entity.Notes = request.Notes.Trim();

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
                d.Type,
                d.Date,
                d.NextDate,
                d.VetCabinetId,
                d.VetCabinet.Name,
                d.VetTimeSlotId,
                d.VetTimeSlot.StartTimeUtc,
                d.VetTimeSlot.EndTimeUtc,
                d.Price,
                d.Currency,
                d.Notes
            );

        private static int GetDefaultIntervalDays(DewormingTypeEnum type)
        {
            return type switch
            {
                DewormingTypeEnum.Internal => 90,
                DewormingTypeEnum.External => 30,
                DewormingTypeEnum.Combined => 90,
                DewormingTypeEnum.Control => 14,
                _ => 90
            };
        }

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