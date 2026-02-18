using Microsoft.EntityFrameworkCore;
using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Services
{
    public sealed class FeedingService : IFeedingService
    {
        private readonly Context _db;

        public FeedingService(Context db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<FeedingResponse>> GetAllForUserAsync(int userId)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));

            var list = await _db.Feedings
                .AsNoTracking()
                .Include(f => f.Animal)
                .Where(f => f.Animal.UserId == userId)
                .OrderByDescending(f => f.Date) // cele mai recente sus
                .ThenBy(f => f.Animal.Name)
                .ToListAsync();

            return list.Select(Map).ToList();
        }

        public async Task<IReadOnlyList<FeedingResponse>> GetAllForAnimalAsync(int userId, int animalId)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (animalId <= 0) throw new ArgumentOutOfRangeException(nameof(animalId));

            var list = await _db.Feedings
                .AsNoTracking()
                .Include(f => f.Animal)
                .Where(f => f.Animal.UserId == userId && f.AnimalId == animalId)
                .OrderByDescending(f => f.Date)
                .ToListAsync();

            return list.Select(Map).ToList();
        }

        public async Task<FeedingResponse> GetByIdAsync(int userId, int feedingId)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (feedingId <= 0) throw new ArgumentOutOfRangeException(nameof(feedingId));

            var entity = await _db.Feedings
                .AsNoTracking()
                .Include(f => f.Animal)
                .FirstOrDefaultAsync(f => f.Id == feedingId && f.Animal.UserId == userId);

            if (entity is null)
                throw new KeyNotFoundException("Feeding not found.");

            return Map(entity);
        }

        public async Task<FeedingResponse> CreateAsync(int userId, CreateFeedingRequest request)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (request is null) throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.Recipe))
                throw new ArgumentException("Recipe is required.");

            if (string.IsNullOrWhiteSpace(request.Quantity))
                throw new ArgumentException("Quantity is required.");

            var animal = await _db.Animals
                .FirstOrDefaultAsync(a => a.Id == request.AnimalId && a.UserId == userId);

            if (animal is null)
                throw new KeyNotFoundException("Animal not found for current user.");

            // dacă nu trimiți date din UI, poți defaulta la now:
            var dateUtc = request.DateUtc == default ? DateTime.UtcNow : request.DateUtc;

            var entity = new Feeding
            {
                AnimalId = animal.Id,
                Recipe = request.Recipe.Trim(),
                Quantity = request.Quantity.Trim(),
                Date = dateUtc,
                Notes = request.Notes?.Trim()
            };

            _db.Feedings.Add(entity);
            await _db.SaveChangesAsync();

            // include animal pentru response
            var created = await _db.Feedings
                .AsNoTracking()
                .Include(f => f.Animal)
                .FirstAsync(f => f.Id == entity.Id);

            return Map(created);
        }

        public async Task<FeedingResponse> UpdateAsync(int userId, int feedingId, UpdateFeedingRequest request)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (feedingId <= 0) throw new ArgumentOutOfRangeException(nameof(feedingId));
            if (request is null) throw new ArgumentNullException(nameof(request));

            var entity = await _db.Feedings
                .Include(f => f.Animal)
                .FirstOrDefaultAsync(f => f.Id == feedingId);

            if (entity is null)
                throw new KeyNotFoundException("Feeding not found.");

            if (entity.Animal.UserId != userId)
                throw new UnauthorizedAccessException("Not allowed.");

            if (request.Recipe is not null)
            {
                if (string.IsNullOrWhiteSpace(request.Recipe))
                    throw new ArgumentException("Recipe cannot be empty.");
                entity.Recipe = request.Recipe.Trim();
            }

            if (request.Quantity is not null)
            {
                if (string.IsNullOrWhiteSpace(request.Quantity))
                    throw new ArgumentException("Quantity cannot be empty.");
                entity.Quantity = request.Quantity.Trim();
            }

            if (request.DateUtc.HasValue)
                entity.Date = request.DateUtc.Value;

            entity.Notes = request.Notes?.Trim();

            await _db.SaveChangesAsync();

            var updated = await _db.Feedings
                .AsNoTracking()
                .Include(f => f.Animal)
                .FirstAsync(f => f.Id == entity.Id);

            return Map(updated);
        }

        public async Task DeleteAsync(int userId, int feedingId)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (feedingId <= 0) throw new ArgumentOutOfRangeException(nameof(feedingId));

            var entity = await _db.Feedings
                .Include(f => f.Animal)
                .FirstOrDefaultAsync(f => f.Id == feedingId);

            if (entity is null) return;

            if (entity.Animal.UserId != userId)
                throw new UnauthorizedAccessException("Not allowed.");

            _db.Feedings.Remove(entity);
            await _db.SaveChangesAsync();
        }

        private static FeedingResponse Map(Feeding f)
            => new(
                f.Id,
                f.AnimalId,
                f.Animal.Name,
                f.Recipe,
                f.Quantity,
                f.Date,
                f.Notes
            );
    }
}
