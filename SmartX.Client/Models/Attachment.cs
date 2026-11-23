
namespace SmartX.Client.Models
{
    public class Attachment
    {
        public Guid Id { get; set; }
        public Guid SensorId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}