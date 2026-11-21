using System.Security.Cryptography;

namespace SmartX.API.Services
{
    public class AttachmentEncryptionService
    {
        private static readonly byte[] Magic = { (byte)'S', (byte)'X', (byte)'E', (byte)'1' };
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int KeySize = 32;

        private readonly byte[] _key;

        public AttachmentEncryptionService(byte[] key)
        {
            if (key == null || key.Length != KeySize)
                throw new ArgumentException("The key must be 32 bytes (256 bits).", nameof(key));

            _key = (byte[])key.Clone();
        }

        public AttachmentEncryptionService(IConfiguration configuration, string dataDirectory)
            : this(LoadOrCreateKey(configuration["Attachments:EncryptionKey"], dataDirectory))
        {
        }

        public byte[] Encrypt(byte[] plaintext, Guid attachmentId)
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var tag = new byte[TagSize];
            var cipher = new byte[plaintext.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Encrypt(nonce, plaintext, cipher, tag, attachmentId.ToByteArray());

            var result = new byte[Magic.Length + NonceSize + TagSize + cipher.Length];
            Magic.CopyTo(result, 0);
            nonce.CopyTo(result, Magic.Length);
            tag.CopyTo(result, Magic.Length + NonceSize);
            cipher.CopyTo(result, Magic.Length + NonceSize + TagSize);
            return result;
        }

        /// <exception cref="CryptographicException">The file is damaged, was changed, or is not an attachment of this id.</exception>
        public byte[] Decrypt(byte[] blob, Guid attachmentId)
        {
            int header = Magic.Length + NonceSize + TagSize;

            if (blob.Length < header || !blob.AsSpan(0, Magic.Length).SequenceEqual(Magic))
                throw new CryptographicException("This is not an encrypted Smart-X attachment.");

            var nonce = blob.AsSpan(Magic.Length, NonceSize);
            var tag = blob.AsSpan(Magic.Length + NonceSize, TagSize);
            var cipher = blob.AsSpan(header);
            var plain = new byte[cipher.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain, attachmentId.ToByteArray());
            return plain;
        }

        private static byte[] LoadOrCreateKey(string? configuredKey, string dataDirectory)
        {
            if (!string.IsNullOrWhiteSpace(configuredKey))
                return Convert.FromBase64String(configuredKey);

            var keyPath = Path.Combine(dataDirectory, "attachments.key");

            if (File.Exists(keyPath))
                return Convert.FromBase64String(File.ReadAllText(keyPath).Trim());

            Directory.CreateDirectory(dataDirectory);
            var key = RandomNumberGenerator.GetBytes(KeySize);
            File.WriteAllText(keyPath, Convert.ToBase64String(key));
            return key;
        }
    }
}
