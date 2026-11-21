using System.Security.Cryptography;
using SmartX.API.Data;
using SmartX.API.Models;

namespace SmartX.API.Services
{
    public record AttachmentDownload(byte[] Content, string FileName, string ContentType);

    public class AttachmentService
    {
        private readonly SmartXDataStore _dataStore;
        private readonly AttachmentEncryptionService _encryption;
        private readonly string _uploadRoot;

        // Extension -> content type we send back on download.
        private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".gif"] = "image/gif",
            [".txt"] = "text/plain",
            [".log"] = "text/plain",
            [".json"] = "application/json",
            [".xml"] = "application/xml",
            [".csv"] = "text/csv"
        };

        private const long MaxFileSize = 10 * 1024 * 1024;

        public AttachmentService(
            SmartXDataStore dataStore,
            IWebHostEnvironment environment,
            AttachmentEncryptionService encryption)
        {
            _dataStore = dataStore;
            _encryption = encryption;
            _uploadRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "uploads"));
        }

        public async Task<Attachment> SaveAttachmentAsync(Guid sensorId, IFormFile file)
        {
            var sensor = _dataStore.Sensors.FirstOrDefault(s => s.Id == sensorId)
                ?? throw new ArgumentException("Sensor not found.");

            if (file == null || file.Length == 0)
                throw new ArgumentException("A file must be provided.");

            if (file.Length > MaxFileSize)
                throw new ArgumentException("File size cannot exceed 10 MB.");

            // Only the last part of the name is kept; anything like "..\..\x.txt" is cut down to "x.txt".
            var displayName = CleanFileName(file.FileName);
            var extension = Path.GetExtension(displayName).ToLowerInvariant();

            if (!AllowedTypes.TryGetValue(extension, out var contentType))
                throw new ArgumentException("This file type is not supported.");

            byte[] content;

            using (var buffer = new MemoryStream())
            {
                await file.CopyToAsync(buffer);
                content = buffer.ToArray();
            }

            if (!MatchesDeclaredType(content, extension))
                throw new ArgumentException("The file content does not match its extension.");

            var attachment = new Attachment
            {
                SensorId = sensor.Id,
                FileName = displayName,
                FileType = extension,
                ContentType = contentType,
                FileSize = content.Length,
                Sha256 = Convert.ToHexString(SHA256.HashData(content)),
                UploadedAt = DateTime.UtcNow
            };

            // The server chooses the path from two GUIDs. Nothing the client sends is part of it.
            var path = StoragePath(attachment.SensorId, attachment.Id);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            await File.WriteAllBytesAsync(path, _encryption.Encrypt(content, attachment.Id));

            _dataStore.AddAttachment(attachment);

            return attachment;
        }

        public IEnumerable<Attachment> GetSensorAttachments(Guid sensorId)
            => _dataStore.GetSensorAttachments(sensorId);

        /// <summary>Decrypts and returns an attachment, or null if it does not exist.</summary>
        /// <exception cref="CryptographicException">The stored file was damaged or changed.</exception>
        public async Task<AttachmentDownload?> ReadAttachmentAsync(Guid sensorId, Guid attachmentId)
        {
            var attachment = _dataStore.FindAttachment(sensorId, attachmentId);

            if (attachment == null)
                return null;

            var path = StoragePath(attachment.SensorId, attachment.Id);

            if (!File.Exists(path))
                return null;

            var content = _encryption.Decrypt(await File.ReadAllBytesAsync(path), attachment.Id);

            return new AttachmentDownload(content, attachment.FileName, attachment.ContentType);
        }

        // uploads/{sensorId}/{attachmentId}.enc, checked to be inside the uploads folder.
        private string StoragePath(Guid sensorId, Guid attachmentId)
        {
            var path = Path.GetFullPath(Path.Combine(_uploadRoot, sensorId.ToString("N"), attachmentId.ToString("N") + ".enc"));

            if (!path.StartsWith(_uploadRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("Attachment path is outside the uploads folder.");

            return path;
        }

        public static string CleanFileName(string? name)
        {
            // Treat both / and \ as separators, whichever system the client runs on.
            var lastPart = (name ?? string.Empty).Split('/', '\\').Last();

            var cleaned = new string(lastPart
                .Where(c => !char.IsControl(c) && Array.IndexOf(Path.GetInvalidFileNameChars(), c) < 0)
                .ToArray())
                .Trim()
                .TrimEnd('.');

            if (cleaned.Length > 100)
            {
                var ext = Path.GetExtension(cleaned);
                cleaned = cleaned[..(100 - ext.Length)] + ext;
            }

            return cleaned;
        }

        // Stops "malware.exe renamed to photo.png": images must start with the right signature bytes.
        private static bool MatchesDeclaredType(byte[] content, string extension) => extension switch
        {
            ".png" => content.Length > 8 && content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47,
            ".jpg" or ".jpeg" => content.Length > 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
            ".gif" => content.Length > 6 && content[0] == 0x47 && content[1] == 0x49 && content[2] == 0x46 && content[3] == 0x38,
            _ => !content.Take(1024).Contains((byte)0)      // text formats must not contain binary NUL bytes
        };
    }
}
