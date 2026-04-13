using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.DB.Enums;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;
using Microsoft.EntityFrameworkCore;

namespace PawPoint.Services.Services
{
    public sealed class AnimalService(Context db, INotificationService notificationService) : IAnimalService
    {
        private readonly Context _db = db;
        private readonly INotificationService _notificationService = notificationService;

        public async Task<AnimalResponse?> GetByIdAsync(int animalId, int userId)
        {
            if (animalId <= 0) throw new ArgumentOutOfRangeException(nameof(animalId));
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));

            var animal = await _db.Animals
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == animalId && a.UserId == userId);

            return animal is null ? null : ToResponse(animal);
        }

        public async Task<IReadOnlyList<AnimalResponse>> GetMyAnimalsAsync(int userId)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));

            var animals = await _db.Animals
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.Name)
                .ToListAsync();

            return animals.Select(ToResponse).ToList();
        }

        public async Task<AnimalResponse> CreateAsync(int userId, CreateAnimalRequest request)
        {
            ValidateUserId(userId);
            ValidateCreate(request);

            var exists = await _db.Animals.AnyAsync(a =>
                a.UserId == userId &&
                a.Name == request.Name &&
                a.Species == request.Species &&
                a.BirthDate == request.BirthDate);

            if (exists)
            {
                throw new InvalidOperationException(
                    "You already have an animal with the same name, species and birth date.");
            }

            var animal = new Animal
            {
                UserId = userId,
                Name = request.Name.Trim(),
                Species = request.Species.Trim(),
                Breed = request.Breed?.Trim(),
                WeightKg = request.WeightKg,
                BirthDate = request.BirthDate,
                Sex = request.Sex?.Trim(),
                MicrochipNumber = request.MicrochipNumber?.Trim(),
                IsDeleted = false
            };

            _db.Animals.Add(animal);
            await _db.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.AnimalCreated,
                    userId,
                    "Animal added",
                    $"{animal.Name} has been added to your account."
                )
            );

            return ToResponse(animal);
        }

        public async Task<AnimalResponse> UpdateAsync(int animalId, int userId, UpdateAnimalRequest request)
        {
            ValidateUserId(userId);
            if (animalId <= 0) throw new ArgumentOutOfRangeException(nameof(animalId));
            request ??= new UpdateAnimalRequest();

            var animal = await _db.Animals
                .FirstOrDefaultAsync(a => a.Id == animalId && a.UserId == userId);

            if (animal is null)
            {
                throw new KeyNotFoundException("Animal not found.");
            }

            if (request.Name is not null)
                animal.Name = request.Name.Trim();

            if (request.Species is not null)
                animal.Species = request.Species.Trim();

            if (request.Breed is not null)
                animal.Breed = request.Breed.Trim();

            if (request.WeightKg.HasValue)
                animal.WeightKg = request.WeightKg;

            if (request.BirthDate.HasValue)
                animal.BirthDate = request.BirthDate;

            if (request.Sex is not null)
                animal.Sex = request.Sex.Trim();

            if (request.MicrochipNumber is not null)
                animal.MicrochipNumber = request.MicrochipNumber.Trim();

            await _db.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.AnimalUpdated,
                    userId,
                    "Animal updated",
                    $"{animal.Name}'s details have been updated."
                )
            );

            return ToResponse(animal);
        }

        public async Task DeleteAsync(int animalId, int userId)
        {
            ValidateUserId(userId);
            if (animalId <= 0) throw new ArgumentOutOfRangeException(nameof(animalId));

            var animal = await _db.Animals
                .FirstOrDefaultAsync(a => a.Id == animalId && a.UserId == userId);

            if (animal is null)
            {
                return;
            }

            var animalName = animal.Name;

            animal.IsDeleted = true;
            await _db.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                userId,
                new NotificationCreateRequest(
                    NotificationTypeEnum.AnimalDeleted,
                    userId,
                    "Animal removed",
                    $"{animalName} has been removed from your account."
                )
            );
        }

        #region Helpers

        private static void ValidateUserId(int userId)
        {
            if (userId <= 0)
                throw new ArgumentOutOfRangeException(nameof(userId), "Invalid user id.");
        }

        private static void ValidateCreate(CreateAnimalRequest req)
        {
            if (req is null) throw new ArgumentNullException(nameof(req));

            if (string.IsNullOrWhiteSpace(req.Name))
                throw new ArgumentException("Name is required.", nameof(req.Name));

            if (string.IsNullOrWhiteSpace(req.Species))
                throw new ArgumentException("Species is required.", nameof(req.Species));
        }

        private static AnimalResponse ToResponse(Animal animal) =>
            new(
                animal.Id,
                animal.Name,
                animal.Species,
                animal.Breed,
                animal.WeightKg,
                animal.BirthDate,
                animal.Sex,
                animal.MicrochipNumber
            );

        #endregion
    }
}