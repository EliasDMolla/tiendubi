namespace Admin.Entities.Entities
{
    public class ProductAsset : Audit
    {
        public int Id { get; set; }
        public int PhotographerEventId { get; set; }
        public string Kind { get; set; } = "digital_file";
        public string OriginalFileName { get; set; } = string.Empty;
        public string ObjectKey { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public long SizeBytes { get; set; }

        public PhotographerEvent PhotographerEvent { get; set; } = null!;
    }
}
