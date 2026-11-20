
namespace SmartX.API.Models
{
    public class CommandRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string DeviceIdentifier { get; set; } = string.Empty;

        public string CommandType { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ExecutedAt { get; set; }

        public bool CanUndo { get; set; }

        public bool IsUndone { get; set; }

        public string? ResultMessage { get; set; }

        // State of the device before the command ran (used by Undo).
        public bool? PreviousState { get; set; }
    }
}
