namespace Admin.Entities.Entities
{
    public class EventPhoto : Audit
    {
        public int Id { get; set; }
        public int PhotographerEventId { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = string.Empty;
        public string? ThumbnailPath { get; set; }
        public string? WatermarkedPath { get; set; }
        public string? Tags { get; set; }
        public bool IsProcessed { get; set; }
        public bool ProcessingFailed { get; set; } = false;
        public string? ProcessingError { get; set; }
        public long SizeBytes { get; set; }
        public bool WatermarkApplied { get; set; } = false;

        public PhotographerEvent PhotographerEvent { get; set; } = null!;
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}