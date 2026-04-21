using Microsoft.EntityFrameworkCore;
using PawPoint.DB;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Models;

namespace PawPoint.Services;

public class AssistantDataService : IAssistantDataService
{
    private readonly Context _context;

    public AssistantDataService(Context context)
    {
        _context = context;
    }

    public async Task<List<PetRecord>> GetPetsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _context.Animals
            .Where(a => a.UserId == userId && !a.IsDeleted)
            .Select(a => new PetRecord
            {
                Id = a.Id,
                Name = a.Name,
                Species = a.Species,
                Breed = a.Breed,
                BirthDate = a.BirthDate,
                WeightKg = a.WeightKg,
                Sex = a.Sex
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PetRecord?> GetPetByNameAsync(int userId, string petName, CancellationToken cancellationToken = default)
    {
        var normalizedPetName = petName.Trim().ToLower();

        return await _context.Animals
            .Where(a =>
                a.UserId == userId &&
                !a.IsDeleted &&
                a.Name.ToLower() == normalizedPetName)
            .Select(a => new PetRecord
            {
                Id = a.Id,
                Name = a.Name,
                Species = a.Species,
                Breed = a.Breed,
                BirthDate = a.BirthDate,
                WeightKg = a.WeightKg,
                Sex = a.Sex
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<AppointmentRecord>> GetUpcomingAppointmentsAsync(
        int userId,
        int daysAhead = 30,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var maxDate = now.AddDays(daysAhead);

        return await _context.Appointments
            .Where(a =>
                a.Animal.UserId == userId &&
                !a.Animal.IsDeleted &&
                a.VetTimeSlot.StartTimeUtc >= now &&
                a.VetTimeSlot.StartTimeUtc <= maxDate)
            .Select(a => new AppointmentRecord
            {
                Id = a.Id,
                PetId = a.AnimalId,
                PetName = a.Animal.Name,
                Type = a.ServiceType,
                ScheduledAt = a.VetTimeSlot.StartTimeUtc,
                VetName = null,
                VetCabinetName = a.VetCabinet.Name,
                Status = a.Status,
                Notes = a.Notes
            })
            .OrderBy(a => a.ScheduledAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AppointmentRecord>> GetAppointmentsForPetAsync(
        int userId,
        int petId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Appointments
            .Where(a =>
                a.Animal.UserId == userId &&
                !a.Animal.IsDeleted &&
                a.AnimalId == petId)
            .Select(a => new AppointmentRecord
            {
                Id = a.Id,
                PetId = a.AnimalId,
                PetName = a.Animal.Name,
                Type = a.ServiceType,
                ScheduledAt = a.VetTimeSlot.StartTimeUtc,
                VetName = null,
                VetCabinetName = a.VetCabinet.Name,
                Status = a.Status,
                Notes = a.Notes
            })
            .OrderByDescending(a => a.ScheduledAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<VaccinationRecord>> GetVaccinationsAsync(
        int userId,
        int? petId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Vaccinations
            .Where(v => v.Animal.UserId == userId && !v.Animal.IsDeleted);

        if (petId.HasValue)
            query = query.Where(v => v.AnimalId == petId.Value);

        return await query
            .Select(v => new VaccinationRecord
            {
                Id = v.Id,
                PetId = v.AnimalId,
                PetName = v.Animal.Name,
                VaccineName = v.VaccineName,
                DateGiven = v.LastDate ?? v.VetTimeSlot.StartTimeUtc,
                NextDueDate = v.NextDate,
                VetName = v.VeterinarianName,
                Notes = v.Notes
            })
            .OrderByDescending(v => v.DateGiven)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DewormingRecord>> GetDewormingsAsync(
        int userId,
        int? petId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Dewormings
            .Where(d => d.Animal.UserId == userId && !d.Animal.IsDeleted);

        if (petId.HasValue)
            query = query.Where(d => d.AnimalId == petId.Value);

        return await query
            .Select(d => new DewormingRecord
            {
                Id = d.Id,
                PetId = d.AnimalId,
                PetName = d.Animal.Name,
                DewormingType = d.Type.ToString(),
                DateGiven = d.Date,
                NextDueDate = d.NextDate,
                IntervalDays = d.IntervalDays,
                ProductName = null,
                VetName = d.VetCabinet.Name,
                Notes = d.Notes
            })
            .OrderByDescending(d => d.DateGiven)
            .ToListAsync(cancellationToken);
    }
}