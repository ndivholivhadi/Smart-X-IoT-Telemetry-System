namespace SmartX.API.Models
{
    public class Attachment
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid SensorId { get; set; }

        // The cleaned-up name shown to users. It is never used as a path on the server.
        public string FileName { get; set; } = string.Empty;

        public string FileType { get; set; } = string.Empty;

        public string ContentType { get; set; } = "application/octet-stream";

        // Size of the original file (the stored, encrypted file is slightly larger).
        public long FileSize { get; set; }

        // SHA-256 of the original file, so a download can be checked against it.
        public string Sha256 { get; set; } = string.Empty;

        public bool Encrypted { get; set; } = true;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
