using PawPoint.DB.Entities;
using Microsoft.EntityFrameworkCore;

namespace PawPoint.DB
{
    public class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        public DbSet<SeedStatus> SeedStatuses { get; set; }
        public DbSet<User> Users => Set<User>();
        public DbSet<VerificationToken> VerificationTokens => Set<VerificationToken>();
        public DbSet<VerificationTokenType> VerificationTokenTypes => Set<VerificationTokenType>();
        public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<NotificationType> NotificationTypes => Set<NotificationType>();
        public DbSet<ImportManifest> ImportManifests => Set<ImportManifest>();
        public DbSet<Animal> Animals => Set<Animal>();
        public DbSet<VetCabinet> VetCabinets => Set<VetCabinet>();
        public DbSet<VetTimeSlot> VetTimeSlots => Set<VetTimeSlot>();
        public DbSet<Appointment> Appointments => Set<Appointment>();
        public DbSet<Vaccination> Vaccinations => Set<Vaccination>();
        public DbSet<Deworming> Dewormings => Set<Deworming>();
        public DbSet<Feeding> Feedings => Set<Feeding>();
        public DbSet<UserSettings> UserSettings => Set<UserSettings>();
        public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
        public DbSet<ContactMessageReply> ContactMessageReplies => Set<ContactMessageReply>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SeedStatus>()
                .Property(x => x.ShouldSeedDatabase)
                .HasDefaultValue(true);

            modelBuilder.Entity<Notification>().HasQueryFilter(n => !n.IsDeleted);           
            modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
            modelBuilder.Entity<VerificationToken>().HasQueryFilter(v => !v.User.IsDeleted);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.EmailHash)
                .IsUnique();

            // User (1) - (many) Animals
            modelBuilder.Entity<Animal>()
                .HasOne(a => a.User)
                .WithMany(u => u.Animals)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // nu returnăm animalele șterse logic
            modelBuilder.Entity<Animal>()
                .HasQueryFilter(a => !a.IsDeleted);

            // prevenim dublurile de același animal la același user
            modelBuilder.Entity<Animal>()
                .HasIndex(a => new { a.UserId, a.Name, a.Species, a.BirthDate })
                .IsUnique();

            // VetCabinet (1) - (many) TimeSlots
            modelBuilder.Entity<VetCabinet>()
                .HasMany(c => c.TimeSlots)
                .WithOne(s => s.VetCabinet)
                .HasForeignKey(s => s.VetCabinetId);

            // VetCabinet (1) - (many) Appointments
            modelBuilder.Entity<VetCabinet>()
                .HasMany(c => c.Appointments)
                .WithOne(a => a.VetCabinet)
                .HasForeignKey(a => a.VetCabinetId);

            // VetTimeSlot (1) - (many) Appointments
            modelBuilder.Entity<VetTimeSlot>()
                .HasMany(s => s.Appointments)
                .WithOne(a => a.VetTimeSlot)
                .HasForeignKey(a => a.VetTimeSlotId);

            // index ca să găsim repede sloturile unui cabinet într-o zi
            modelBuilder.Entity<VetTimeSlot>()
                .HasIndex(s => new { s.VetCabinetId, s.StartTimeUtc });

            // Animal (1) - (many) Appointments
            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Animal)
                .WithMany(an => an.Appointments)
                .HasForeignKey(a => a.AnimalId);

            // Animal (1) - (many) Vaccinations
            modelBuilder.Entity<Vaccination>()
                .HasOne(v => v.Animal)
                .WithMany(a => a.Vaccinations)
                .HasForeignKey(v => v.AnimalId);

            // Animal (1) - (many) Dewormings
            modelBuilder.Entity<Deworming>()
                .HasOne(d => d.Animal)
                .WithMany(a => a.Dewormings)
                .HasForeignKey(d => d.AnimalId);

            // Animal (1) - (many) Feedings
            modelBuilder.Entity<Feeding>()
                .HasOne(f => f.Animal)
                .WithMany(a => a.Feedings)
                .HasForeignKey(f => f.AnimalId);

            modelBuilder.Entity<UserSettings>()
                .HasOne(s => s.User)
                .WithOne(u => u.Settings)
                .HasForeignKey<UserSettings>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ContactMessage>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<ContactMessage>()
                .HasOne(x => x.User)
                .WithMany(u => u.ContactMessages)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ContactMessageReply>()
                .HasOne(x => x.ContactMessage)
                .WithMany(x => x.Replies)
                .HasForeignKey(x => x.ContactMessageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ContactMessageReply>()
                .HasOne(x => x.SenderUser)
                .WithMany(u => u.ContactMessageReplies)
                .HasForeignKey(x => x.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}