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

            var animal = await _db.Animals
                .FirstOrDefaultAsync(a => a.Id == request.AnimalId && a.UserId == userId);

            if (animal is null) throw new KeyNotFoundException("Animal not found for current user.");

            var cabinet = await _db.VetCabinets
                .FirstOrDefaultAsync(c => c.Id == request.VetCabinetId);

            if (cabinet is null) throw new KeyNotFoundException("Vet cabinet not found.");

            var slot = await _db.VetTimeSlots
                .FirstOrDefaultAsync(s => s.Id == request.VetTimeSlotId);

            if (slot is null) throw new KeyNotFoundException("Time slot not found.");

            if (slot.VetCabinetId != request.VetCabinetId)
                throw new ArgumentException("Selected slot does not belong to selected cabinet.");

            if (slot.BookedCount >= slot.Capacity)
                throw new InvalidOperationException("Selected slot is full.");

            if (slot.StartTimeUtc <= DateTime.UtcNow)
                throw new InvalidOperationException("You can only book future slots.");

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

            await _notificationService.CreateNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.VaccinationBooked,
                    userId,
                    "Vaccination booked",
                    $"{animal.Name} has been scheduled for {vaccination.VaccineName} on {slot.StartTimeUtc:dd.MM.yyyy}. VAX:{vaccination.Id}"
                )
            );

            await ScheduleVaccinationRemindersAsync(
                userId,
                animal.Name,
                vaccination.VaccineName,
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

            await _notificationService.CreateNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.VaccinationUpdated,
                    userId,
                    "Vaccination updated",
                    $"{vax.Animal.Name}'s vaccination ({vax.VaccineName}) has been updated. VAX:{vax.Id}"
                )
            );

            await ScheduleVaccinationRemindersAsync(
                userId,
                vax.Animal.Name,
                vax.VaccineName,
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

            if (vax is null) return;
            if (vax.Animal.UserId != userId) throw new UnauthorizedAccessException("Not allowed.");

            await _notificationService.CreateNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.VaccinationCancelled,
                    userId,
                    "Vaccination removed",
                    $"{vax.Animal.Name}'s vaccination ({vax.VaccineName}) has been removed. VAX:{vax.Id}"
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