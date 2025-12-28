namespace PawPoint.DB.Entities
{
    public class ImportManifest
    {
        public required int Id { get; set; }
        public required string FileName { get; set; }
        public DateTime ImportedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public bool IsProcessed { get; set; } = false;
    }
}
