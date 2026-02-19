using Microsoft.EntityFrameworkCore;
using PawPoint.DB;
using PawPoint.DB.Enums;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using SystemTask = System.Threading.Tasks.Task;

namespace PawPoint.Services.Jobs
{
    public class DailyNotificationJob(Context db, INotificationService notifications)
    {
        public async SystemTask Run()
        {
            var todayUtc = DateTime.UtcNow.Date;
            var tomorrowUtc = todayUtc.AddDays(1);
            var dueWindowEndUtc = todayUtc.AddDays(7); // schimbă dacă vrei 3/14/etc.

            var appts = await db.Appointments
                .AsNoTracking()
                .Include(a => a.Animal)
                .Include(a => a.VetCabinet)
                .Include(a => a.VetTimeSlot)
                .Where(a =>
                    a.Animal.UserId > 0 &&
                    a.VetTimeSlot.StartTimeUtc.Date == tomorrowUtc)
                .Select(a => new
                {
                    a.Animal.UserId,
                    AppointmentId = a.Id,
                    AnimalName = a.Animal.Name,
                    CabinetName = a.VetCabinet.Name,
                    StartUtc = a.VetTimeSlot.StartTimeUtc
                })
                .ToListAsync();

            foreach (var a in appts)
            {
                var exists = await db.Notifications.AnyAsync(n =>
                    n.UserId == a.UserId &&
                    n.NotificationTypeId == (int)NotificationTypeEnum.AppointmentReminder &&
                    n.CreatedAt.Date == todayUtc &&
                    n.Content.Contains($"APPT:{a.AppointmentId}"));

                if (exists) continue;

                await notifications.CreateNotificationAsync(
                    a.UserId,
                    new NotificationCreateRequest(
                        NotificationTypeEnum.AppointmentReminder,
                        a.UserId,
                        "Appointment reminder",
                        $"Tomorrow you have an appointment for {a.AnimalName} at {a.StartUtc:HH:mm} ({a.CabinetName}). APPT:{a.AppointmentId}"
                    ),
                    systemRun: true
                );
            }

            var vaxDue = await db.Vaccinations
                .AsNoTracking()
                .Include(v => v.Animal)
                .Where(v =>
                    v.Animal.UserId > 0 &&
                    v.NextDate.HasValue &&
                    v.NextDate.Value.Date >= todayUtc &&
                    v.NextDate.Value.Date <= dueWindowEndUtc)
                .Select(v => new
                {
                    v.Animal.UserId,
                    VaccinationId = v.Id,
                    AnimalName = v.Animal.Name,
                    v.VaccineName,
                    NextDate = v.NextDate!.Value
                })
                .ToListAsync();

            foreach (var v in vaxDue)
            {
                var exists = await db.Notifications.AnyAsync(n =>
                    n.UserId == v.UserId &&
                    n.NotificationTypeId == (int)NotificationTypeEnum.VaccinationDue &&
                    n.CreatedAt.Date == todayUtc &&
                    n.Content.Contains($"VAX:{v.VaccinationId}"));

                if (exists) continue;

                await notifications.CreateNotificationAsync(
                    v.UserId,
                    new NotificationCreateRequest(
                        NotificationTypeEnum.VaccinationDue,
                        v.UserId,
                        "Vaccination due soon",
                        $"{v.AnimalName} needs {v.VaccineName} around {v.NextDate:dd.MM.yyyy}. VAX:{v.VaccinationId}"
                    ),
                    systemRun: true
                );
            }

            var dewDue = await db.Dewormings
                .AsNoTracking()
                .Include(d => d.Animal)
                .Where(d =>
                    d.Animal.UserId > 0 &&
                    d.NextDate.HasValue &&
                    d.NextDate.Value.Date >= todayUtc &&
                    d.NextDate.Value.Date <= dueWindowEndUtc)
                .Select(d => new
                {
                    d.Animal.UserId,
                    DewormingId = d.Id,
                    AnimalName = d.Animal.Name,
                    Type = d.Type.ToString(),
                    NextDate = d.NextDate!.Value
                })
                .ToListAsync();

            foreach (var d in dewDue)
            {
                var exists = await db.Notifications.AnyAsync(n =>
                    n.UserId == d.UserId &&
                    n.NotificationTypeId == (int)NotificationTypeEnum.DewormingDue &&
                    n.CreatedAt.Date == todayUtc &&
                    n.Content.Contains($"DEW:{d.DewormingId}"));

                if (exists) continue;

                await notifications.CreateNotificationAsync(
                    d.UserId,
                    new NotificationCreateRequest(
                        NotificationTypeEnum.DewormingDue,
                        d.UserId,
                        "Deworming due soon",
                        $"{d.AnimalName} needs deworming ({d.Type}) around {d.NextDate:dd.MM.yyyy}. DEW:{d.DewormingId}"
                    ),
                    systemRun: true
                );
            }
        }
    }
}
