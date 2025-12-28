using System.ComponentModel.DataAnnotations;

namespace PawPoint.DB.Entities
{
    public class Feeding
    {
        [Key]
        public int Id { get; set; }

        public int AnimalId { get; set; }
        public Animal Animal { get; set; } = null!;

        [MaxLength(200)]
        public required string Recipe { get; set; }      

        [MaxLength(50)]
        public string? Quantity { get; set; }            

        public DateTime Date { get; set; }              

        [MaxLength(200)]
        public string? Notes { get; set; }          
    }
}
