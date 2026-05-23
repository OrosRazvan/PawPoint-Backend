using PawPoint.DB.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PawPoint.DB.Entities
{
    public class Appointment
    {
        [Key]
        public int Id { get; set; }

        public int AnimalId { get; set; }
        public Animal Animal { get; set; } = null!;

        public int VetCabinetId { get; set; }
        public VetCabinet VetCabinet { get; set; } = null!;

        public int VetTimeSlotId { get; set; }
        public VetTimeSlot VetTimeSlot { get; set; } = null!;

        [MaxLength(100)]
        public required string ServiceType { get; set; }   // Consult, Vaccination, etc.

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public decimal? Price { get; set; }
        public Currency Currency { get; set; } = Currency.Eur;

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Confirmed";

        public bool Notify24hInAdvance { get; set; } = false;
    }
}
