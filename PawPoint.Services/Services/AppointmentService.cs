using Microsoft.EntityFrameworkCore;
using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Services
{
    public sealed class AppointmentService : IAppointmentService
    {
        private readonly Context _db;

        public AppointmentService(Context db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<VetCabinetListItemResponse>> GetVetCabinetsAsync(
            string? serviceType,
            string? sortBy)
        {
            // pentru început ignorăm serviceType, dar îl păstrăm dacă vrei să filtrezi prețul mai târziu
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
            var toUtc = toDate
                .ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc);

            var slots = await _db.VetTimeSlots
                .AsNoTracking()
                .Where(s =>
                    s.VetCabinetId == vetCabinetId &&
                    s.StartTimeUtc >= fromUtc &&
                    s.StartTimeUtc <= toUtc &&
                    s.BookedCount < s.Capacity)
                .OrderBy(s => s.StartTimeUtc)
                .ToListAsync();

            var days = slots
                .GroupBy(s => DateOnly.FromDateTime(s.StartTimeUtc))
                .Select(g => new VetDayAvailabilityResponse(
                    g.Key,
                    g.Select(s => new VetSlotResponse(
                        s.Id,
                        s.StartTimeUtc,
                        s.EndTimeUtc,
                        true
                    )).ToList()
                ))
                .OrderBy(d => d.Date)
                .ToList();

            return new VetAvailabilityResponse(
                cabinet.Id,
                cabinet.Name,
                days
            );
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
                    a.VetTimeSlot.EndTimeUtc >= cutoff) // doar ultimele 15 zile + viitor
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

            // verificăm animalul să aparțină user-ului (similar cu AnimalService) :contentReference[oaicite:1]{index=1}
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

            // opțional, validare extra că slotul chiar aparține cabinetului
            if (slot.VetCabinetId != request.VetCabinetId)
                throw new InvalidOperationException("Selected time slot does not belong to the specified cabinet.");

            if (slot.BookedCount >= slot.Capacity)
                throw new InvalidOperationException("Selected time slot is no longer available.");

            // creezi appointment-ul
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

            // marchezi slotul ca ocupat (sau crești BookedCount)
            slot.BookedCount++;

            await _db.SaveChangesAsync();

            return new AppointmentResponse(
                appointment.Id,
                animal.Id,
                animal.Name,
                cabinet.Id,
                cabinet.Name,
                slot.Id,
                slot.StartTimeUtc,
                slot.EndTimeUtc,
                appointment.ServiceType,
                appointment.EstimatedPriceRon,
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
            var cutoff = nowUtc.AddDays(-15); // nu mai vechi de 15 zile

            var list = await QueryAnimalAppointments(animalId, userId)
                .Where(a =>
                    a.VetTimeSlot.StartTimeUtc < nowUtc &&              // trecut
                    a.VetTimeSlot.EndTimeUtc >= cutoff)                 // dar în ultimele 15 zile
                .OrderByDescending(a => a.VetTimeSlot.StartTimeUtc)     // cele mai recente primele
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

            // schimbăm slotul, dacă a fost trimis altul
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

                // eliberăm slotul vechi
                appointment.VetTimeSlot.BookedCount =
                    Math.Max(0, appointment.VetTimeSlot.BookedCount - 1);

                // ocupăm slotul nou
                newSlot.BookedCount++;

                appointment.VetTimeSlotId = newSlot.Id;
                appointment.VetTimeSlot = newSlot;
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
               a.VetTimeSlotId,
               a.VetTimeSlot.StartTimeUtc,
               a.VetTimeSlot.EndTimeUtc,
               a.ServiceType,
               a.EstimatedPriceRon,
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
            // tot ce s-a terminat cu mai mult de 15 zile în urmă
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
        #endregion
    }
}
