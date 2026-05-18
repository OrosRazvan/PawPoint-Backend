using Microsoft.EntityFrameworkCore;
using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.DB.Enums;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Services
{
    public sealed class AppointmentService(Context db, INotificationService notificationService) : IAppointmentService
    {
        private readonly Context _db = db;
        private readonly INotificationService _notificationService = notificationService;

        public async Task<IReadOnlyList<VetCabinetListItemResponse>> GetVetCabinetsAsync(
            string? serviceType,
            string? sortBy)
        {
            var query = _db.VetCabinets.AsNoTracking();

            query = sortBy?.ToLower() switch
            {
                "price" => query.OrderBy(c => c.BasePriceRon),
                "rating" => query.OrderByDescending(c => c.Rating),
                "distance" => query.OrderBy(c => c.DistanceKm),
                _ => query.OrderBy(c => c.Name)
            };

            var cabinets = await query.ToListAsync();

            return cabinets.Select(c => new VetCabinetListItemResponse(
                c.Id,
                c.Name,
                c.Address,
                c.City,
                c.PhoneNumber,
                c.Website,
                c.Rating,
                c.DistanceKm,
                c.BasePriceRon
            )).ToList();
        }

        public async Task<VetAvailabilityResponse> GetAvailabilityAsync(
            int userId,
            int vetCabinetId,
            DateOnly fromDate,
            DateOnly toDate)
        {
            if (fromDate > toDate)
                throw new ArgumentException("fromDate must be <= toDate");

            var cabinet = await _db.VetCabinets
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == vetCabinetId);

            if (cabinet is null)
                throw new KeyNotFoundException("Vet cabinet not found.");

            var fromUtc = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var toUtc = toDate.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc);

            var slots = await _db.VetTimeSlots
                .AsNoTracking()
                .Where(s =>
                    s.VetCabinetId == vetCabinetId &&
                    s.StartTimeUtc >= fromUtc &&
                    s.StartTimeUtc <= toUtc)
                .OrderBy(s => s.StartTimeUtc)
                .ToListAsync();

            var slotIds = slots.Select(s => s.Id).ToList();

            var bookedCounts = await _db.Appointments
                .AsNoTracking()
                .Where(a =>
                    slotIds.Contains(a.VetTimeSlotId) &&
                    a.Status != "Cancelled")
                .GroupBy(a => a.VetTimeSlotId)
                .Select(g => new
                {
                    VetTimeSlotId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.VetTimeSlotId, x => x.Count);

            var userBookedSlotIds = await _db.Appointments
                .AsNoTracking()
                .Where(a =>
                    slotIds.Contains(a.VetTimeSlotId) &&
                    a.Animal.UserId == userId &&
                    a.Status != "Cancelled")
                .Select(a => a.VetTimeSlotId)
                .ToListAsync();

            var availableSlots = slots
                .Where(s => !userBookedSlotIds.Contains(s.Id))
                .Select(slot =>
                {
                    var bookedCount = bookedCounts.TryGetValue(slot.Id, out var count)
                        ? count
                        : 0;

                    var availableCount = Math.Max(0, slot.Capacity - bookedCount);

                    return new
                    {
                        Slot = slot,
                        BookedCount = bookedCount,
                        AvailableCount = availableCount
                    };
                })
                .Where(x => x.AvailableCount > 0)
                .ToList();

            var days = availableSlots
                .GroupBy(x => DateOnly.FromDateTime(x.Slot.StartTimeUtc))
                .Select(g => new VetDayAvailabilityResponse(
                    g.Key,
                    g.Select(x => new VetSlotResponse(
                        x.Slot.Id,
                        x.Slot.StartTimeUtc,
                        x.Slot.EndTimeUtc,
                        x.Slot.Capacity,
                        x.BookedCount,
                        x.AvailableCount
                    )).ToList()
                ))
                .OrderBy(d => d.Date)
                .ToList();

            return new VetAvailabilityResponse(cabinet.Id, cabinet.Name, days);
        }

        public async Task<IReadOnlyList<AppointmentResponse>> GetAllForUserAsync(int userId)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));

            await CleanupOldAppointmentsAsync();

            var nowUtc = DateTime.UtcNow;
            var cutoff = nowUtc.AddDays(-15);

            var list = await _db.Appointments
                .AsNoTracking()
                .Include(a => a.Animal)
                .Include(a => a.VetCabinet)
                .Include(a => a.VetTimeSlot)
                .Where(a =>
                    a.Animal.UserId == userId &&
                    a.VetTimeSlot.EndTimeUtc >= cutoff)
                .OrderByDescending(a => a.VetTimeSlot.StartTimeUtc)
                .ToListAsync();

            return list.Select(MapAppointment).ToList();
        }

        public async Task<AppointmentResponse> GetByIdAsync(int userId, int appointmentId)
        {
            if (appointmentId <= 0)
                throw new ArgumentOutOfRangeException(nameof(appointmentId));
            if (userId <= 0)
                throw new ArgumentOutOfRangeException(nameof(userId));

            var appointment = await _db.Appointments
                .AsNoTracking()
                .Include(a => a.Animal)
                .Include(a => a.VetCabinet)
                .Include(a => a.VetTimeSlot)
                .FirstOrDefaultAsync(a =>
                    a.Id == appointmentId &&
                    a.Animal.UserId == userId);

            if (appointment is null)
                throw new KeyNotFoundException("Appointment not found.");

            return MapAppointment(appointment);
        }

        public async Task<AppointmentResponse> CreateAsync(
            int userId,
            CreateAppointmentRequest request)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (request is null) throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.ServiceType))
                throw new ArgumentException("ServiceType is required.", nameof(request.ServiceType));

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
                throw new KeyNotFoundException("Selected time slot not found.");

            if (slot.VetCabinetId != request.VetCabinetId)
                throw new InvalidOperationException("Selected time slot does not belong to the specified cabinet.");

            var bookedCount = await _db.Appointments
            .CountAsync(a =>
                a.VetTimeSlotId == slot.Id &&
                a.Status != "Cancelled");

            if (bookedCount >= slot.Capacity)
                throw new InvalidOperationException("Selected time slot is no longer available.");

            var appointment = new Appointment
            {
                AnimalId = animal.Id,
                VetCabinetId = slot.VetCabinetId,
                VetTimeSlotId = slot.Id,
                ServiceType = request.ServiceType.Trim(),
                EstimatedPriceRon = request.EstimatedPriceRon ?? cabinet.BasePriceRon,
                Notes = request.Notes?.Trim(),
                Notify24hInAdvance = request.Notify24hInAdvance,
                Status = "Confirmed",
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.Appointments.Add(appointment);
            slot.BookedCount++;

            await _db.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.AppointmentBooked,
                    userId,
                    "Appointment booked",
                    $"{animal.Name} has been scheduled for {appointment.ServiceType} on {slot.StartTimeUtc:dd.MM.yyyy} at {slot.StartTimeUtc:HH:mm} ({cabinet.Name}). APPT:{appointment.Id}"
                )
            );

            await ScheduleAppointmentRemindersAsync(
                userId,
                animal.Name,
                cabinet.Name,
                appointment.Id,
                slot.StartTimeUtc
            );

            return new AppointmentResponse(
                appointment.Id,
                animal.Id,
                animal.Name,
                cabinet.Id,
                cabinet.Name,
                $"{cabinet.Address}, {cabinet.City}",
                slot.Id,
                slot.StartTimeUtc,
                slot.EndTimeUtc,
                appointment.ServiceType,
                appointment.EstimatedPriceRon ?? cabinet.BasePriceRon,
                appointment.Status,
                appointment.Notify24hInAdvance
            );
        }

        public async Task<IReadOnlyList<AppointmentResponse>> GetAnimalPastAppointmentsAsync(
            int animalId,
            int userId)
        {
            if (animalId <= 0) throw new ArgumentOutOfRangeException(nameof(animalId));

            await CleanupOldAppointmentsAsync();

            var nowUtc = DateTime.UtcNow;
            var cutoff = nowUtc.AddDays(-15);

            var list = await QueryAnimalAppointments(animalId, userId)
                .Where(a =>
                    a.VetTimeSlot.StartTimeUtc < nowUtc &&
                    a.VetTimeSlot.EndTimeUtc >= cutoff)
                .OrderByDescending(a => a.VetTimeSlot.StartTimeUtc)
                .ToListAsync();

            return list.Select(MapAppointment).ToList();
        }

        public async Task<IReadOnlyList<AppointmentResponse>> GetAnimalUpcomingAppointmentsAsync(
            int animalId,
            int userId)
        {
            if (animalId <= 0) throw new ArgumentOutOfRangeException(nameof(animalId));

            await CleanupOldAppointmentsAsync();

            var nowUtc = DateTime.UtcNow;

            var list = await QueryAnimalAppointments(animalId, userId)
                .Where(a => a.VetTimeSlot.StartTimeUtc >= nowUtc)
                .OrderBy(a => a.VetTimeSlot.StartTimeUtc)
                .ToListAsync();

            return list.Select(MapAppointment).ToList();
        }

        public async Task<AppointmentResponse> UpdateAsync(
            int appointmentId,
            int userId,
            UpdateAppointmentRequest request)
        {
            if (appointmentId <= 0) throw new ArgumentOutOfRangeException(nameof(appointmentId));
            if (request is null) throw new ArgumentNullException(nameof(request));

            var appointment = await _db.Appointments
                .Include(a => a.Animal)
                .Include(a => a.VetCabinet)
                .Include(a => a.VetTimeSlot)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment is null)
                throw new KeyNotFoundException("Appointment not found.");

            if (appointment.Animal.UserId != userId)
                throw new UnauthorizedAccessException("You cannot modify this appointment.");

            var slotChanged = false;

            if (request.VetTimeSlotId.HasValue &&
                request.VetTimeSlotId.Value != appointment.VetTimeSlotId)
            {
                var newSlot = await _db.VetTimeSlots
                    .FirstOrDefaultAsync(s =>
                        s.Id == request.VetTimeSlotId.Value &&
                        s.VetCabinetId == appointment.VetCabinetId);

                if (newSlot is null)
                    throw new KeyNotFoundException("New time slot not found.");

                if (newSlot.BookedCount >= newSlot.Capacity)
                    throw new InvalidOperationException("New time slot is fully booked.");

                appointment.VetTimeSlot.BookedCount =
                    Math.Max(0, appointment.VetTimeSlot.BookedCount - 1);

                newSlot.BookedCount++;

                appointment.VetTimeSlotId = newSlot.Id;
                appointment.VetTimeSlot = newSlot;
                slotChanged = true;
            }

            if (request.EstimatedPriceRon.HasValue)
                appointment.EstimatedPriceRon = request.EstimatedPriceRon.Value;

            if (request.Notes is not null)
                appointment.Notes = request.Notes.Trim();

            if (!string.IsNullOrWhiteSpace(request.Status))
                appointment.Status = request.Status.Trim();

            if (request.Notify24hInAdvance.HasValue)
                appointment.Notify24hInAdvance = request.Notify24hInAdvance.Value;

            await _db.SaveChangesAsync();

            var updated = await _db.Appointments
                .AsNoTracking()
                .Include(a => a.Animal)
                .Include(a => a.VetCabinet)
                .Include(a => a.VetTimeSlot)
                .FirstAsync(a => a.Id == appointment.Id);

            if (slotChanged)
            {
                await _notificationService.CreateNotificationAsync(
                    userId,
                    new NotificationCreateRequest(
                        NotificationTypeEnum.AppointmentRescheduled,
                        userId,
                        "Appointment rescheduled",
                        $"{updated.Animal.Name}'s appointment has been moved to {updated.VetTimeSlot.StartTimeUtc:dd.MM.yyyy} at {updated.VetTimeSlot.StartTimeUtc:HH:mm} ({updated.VetCabinet.Name}). APPT:{updated.Id}"
                    )
                );

                await ScheduleAppointmentRemindersAsync(
                    userId,
                    updated.Animal.Name,
                    updated.VetCabinet.Name,
                    updated.Id,
                    updated.VetTimeSlot.StartTimeUtc
                );
            }

            return MapAppointment(updated);
        }

        #region Helpers

        private static AppointmentResponse MapAppointment(Appointment a)
           => new(
               a.Id,
               a.AnimalId,
               a.Animal.Name,
               a.VetCabinetId,
               a.VetCabinet.Name,
               $"{a.VetCabinet.Address}, {a.VetCabinet.City}",
               a.VetTimeSlotId,
               a.VetTimeSlot.StartTimeUtc,
               a.VetTimeSlot.EndTimeUtc,
               a.ServiceType,
               a.EstimatedPriceRon ?? a.VetCabinet.BasePriceRon,
               a.Status,
               a.Notify24hInAdvance
           );

        private IQueryable<Appointment> QueryAnimalAppointments(int animalId, int userId)
        {
            return _db.Appointments
                .AsNoTracking()
                .Include(a => a.Animal)
                .Include(a => a.VetCabinet)
                .Include(a => a.VetTimeSlot)
                .Where(a => a.AnimalId == animalId && a.Animal.UserId == userId);
        }

        private async Task CleanupOldAppointmentsAsync()
        {
            var cutoff = DateTime.UtcNow.AddDays(-15);

            var oldAppointments = await _db.Appointments
                .Include(a => a.VetTimeSlot)
                .Where(a => a.VetTimeSlot.EndTimeUtc < cutoff)
                .ToListAsync();

            if (oldAppointments.Count == 0)
                return;

            _db.Appointments.RemoveRange(oldAppointments);
            await _db.SaveChangesAsync();
        }

        private async Task ScheduleAppointmentRemindersAsync(
            int userId,
            string animalName,
            string cabinetName,
            int appointmentId,
            DateTime slotStartUtc)
        {
            await _notificationService.ScheduleNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.AppointmentReminder,
                    userId,
                    "Appointment reminder",
                    $"In 7 days you have an appointment for {animalName} at {slotStartUtc:HH:mm} ({cabinetName}). APPT:{appointmentId}"
                ),
                slotStartUtc.AddDays(-7)
            );

            await _notificationService.ScheduleNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.AppointmentReminder,
                    userId,
                    "Appointment reminder",
                    $"Tomorrow you have an appointment for {animalName} at {slotStartUtc:HH:mm} ({cabinetName}). APPT:{appointmentId}"
                ),
                slotStartUtc.AddDays(-1)
            );
        }

        #endregion
    }
}