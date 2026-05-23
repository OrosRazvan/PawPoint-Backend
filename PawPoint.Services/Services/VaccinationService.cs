using Microsoft.EntityFrameworkCore;
using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.DB.Enums;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Services
{
    public sealed class VaccinationService(Context db, INotificationService notificationService) : IVaccinationService
    {
        private readonly Context _db = db;
        private readonly INotificationService _notificationService = notificationService;

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
                .ThenBy(v => v.VaccineType)
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
                .ThenBy(v => v.VaccineType)
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

            if (vax is null)
                throw new KeyNotFoundException("Vaccination not found.");

            return Map(vax);
        }

        public async Task<VaccinationResponse> CreateAsync(int userId, CreateVaccinationRequest request)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            if (userId <= 0)
                throw new ArgumentOutOfRangeException(nameof(userId));

            if (request.AnimalId <= 0)
                throw new ArgumentException("AnimalId is required.");

            if (request.VetCabinetId <= 0)
                throw new ArgumentException("VetCabinetId is required.");

            if (request.VetTimeSlotId <= 0)
                throw new ArgumentException("VetTimeSlotId is required.");

            if (request.LastDate.HasValue && request.NextDate.HasValue &&
                request.NextDate.Value < request.LastDate.Value)
                throw new ArgumentException("NextDateUtc must be >= LastDateUtc.");

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
                    p.ServiceType == "Vaccination" &&
                    p.VaccineType == request.VaccineType);

            slot.BookedCount += 1;

            var vaccination = new Vaccination
            {
                AnimalId = animal.Id,
                VaccineType = request.VaccineType,
                VetCabinetId = cabinet.Id,
                VetTimeSlotId = slot.Id,
                LastDate = request.LastDate,
                NextDate = request.NextDate,
                Price = price?.Price,
                Currency = price?.Currency ?? Currency.Eur,
                Notes = request.Notes?.Trim()
            };

            _db.Vaccinations.Add(vaccination);
            await _db.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.VaccinationBooked,
                    userId,
                    "Vaccination booked",
                    $"{animal.Name} has been scheduled for {vaccination.VaccineType} on {slot.StartTimeUtc:dd.MM.yyyy}. VAX:{vaccination.Id}"
                )
            );

            await ScheduleVaccinationRemindersAsync(
                userId,
                animal.Name,
                vaccination.VaccineType.ToString(),
                vaccination.Id,
                vaccination.NextDate
            );

            var created = await _db.Vaccinations
                .AsNoTracking()
                .Include(v => v.Animal)
                .Include(v => v.VetCabinet)
                .Include(v => v.VetTimeSlot)
                .FirstAsync(v => v.Id == vaccination.Id);

            return Map(created);
        }

        public async Task<VaccinationResponse> UpdateAsync(
    int userId,
    int vaccinationId,
    UpdateVaccinationRequest request)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            var vax = await _db.Vaccinations
                .Include(v => v.Animal)
                .Include(v => v.VetCabinet)
                .Include(v => v.VetTimeSlot)
                .FirstOrDefaultAsync(v => v.Id == vaccinationId);

            if (vax is null)
                throw new KeyNotFoundException("Vaccination not found.");

            if (vax.Animal.UserId != userId)
                throw new UnauthorizedAccessException("Not allowed.");

            if (request.AnimalId.HasValue && request.AnimalId.Value != vax.AnimalId)
            {
                var animal = await _db.Animals.FirstOrDefaultAsync(a =>
                    a.Id == request.AnimalId.Value &&
                    a.UserId == userId &&
                    !a.IsDeleted);

                if (animal is null)
                    throw new KeyNotFoundException("Animal not found for current user.");

                vax.AnimalId = animal.Id;
                vax.Animal = animal;
            }

            if (request.VaccineType.HasValue)
                vax.VaccineType = request.VaccineType.Value;

            if (request.VetTimeSlotId.HasValue &&
                request.VetTimeSlotId.Value != vax.VetTimeSlotId)
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

                vax.VetTimeSlot.BookedCount =
                    Math.Max(0, vax.VetTimeSlot.BookedCount - 1);

                newSlot.BookedCount += 1;

                vax.VetTimeSlotId = newSlot.Id;
                vax.VetTimeSlot = newSlot;

                vax.VetCabinetId = newSlot.VetCabinetId;
                vax.VetCabinet = newSlot.VetCabinet;

                vax.LastDate = newSlot.StartTimeUtc;
            }
            else if (request.VetCabinetId.HasValue &&
                     request.VetCabinetId.Value != vax.VetCabinetId)
            {
                throw new InvalidOperationException("To change the cabinet, select a new time slot from that cabinet.");
            }

            if (request.LastDate.HasValue)
                vax.LastDate = request.LastDate.Value;

            if (request.NextDate.HasValue)
                vax.NextDate = request.NextDate.Value;

            if (vax.LastDate.HasValue &&
                vax.NextDate.HasValue &&
                vax.NextDate.Value < vax.LastDate.Value)
                throw new ArgumentException("NextDateUtc must be >= LastDateUtc.");

            var price = await _db.VetServicePrices
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.VetCabinetId == vax.VetCabinetId &&
                    p.ServiceType == "Vaccination" &&
                    p.VaccineType == vax.VaccineType);

            vax.Price = price?.Price;
            vax.Currency = price?.Currency ?? Currency.Eur;

            if (request.Notes is not null)
                vax.Notes = request.Notes.Trim();

            await _db.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.VaccinationUpdated,
                    userId,
                    "Vaccination updated",
                    $"{vax.Animal.Name}'s vaccination ({vax.VaccineType}) has been updated. VAX:{vax.Id}"
                )
            );

            await ScheduleVaccinationRemindersAsync(
                userId,
                vax.Animal.Name,
                vax.VaccineType.ToString(),
                vax.Id,
                vax.NextDate
            );

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

            if (vax is null)
                return;

            if (vax.Animal.UserId != userId)
                throw new UnauthorizedAccessException("Not allowed.");

            await _notificationService.CreateNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.VaccinationCancelled,
                    userId,
                    "Vaccination removed",
                    $"{vax.Animal.Name}'s vaccination ({vax.VaccineType}) has been removed. VAX:{vax.Id}"
                )
            );

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
                v.VaccineType,
                v.LastDate,
                v.NextDate,
                v.VetCabinetId,
                v.VetCabinet.Name,
                v.VetTimeSlotId,
                v.VetTimeSlot.StartTimeUtc,
                v.VetTimeSlot.EndTimeUtc,
                v.Price,
                v.Currency,
                v.Notes
            );

        private async Task ScheduleVaccinationRemindersAsync(
            int userId,
            string animalName,
            string vaccineName,
            int vaccinationId,
            DateTime? nextDateUtc)
        {
            if (!nextDateUtc.HasValue)
                return;

            await _notificationService.ScheduleNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.VaccinationReminder,
                    userId,
                    "Vaccination reminder",
                    $"{animalName} needs {vaccineName} in 7 days, around {nextDateUtc.Value:dd.MM.yyyy}. VAX:{vaccinationId}"
                ),
                nextDateUtc.Value.AddDays(-7)
            );

            await _notificationService.ScheduleNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.VaccinationReminder,
                    userId,
                    "Vaccination reminder",
                    $"{animalName} needs {vaccineName} tomorrow, around {nextDateUtc.Value:dd.MM.yyyy}. VAX:{vaccinationId}"
                ),
                nextDateUtc.Value.AddDays(-1)
            );
        }
    }
}