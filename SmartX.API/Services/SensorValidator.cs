using System.Text.RegularExpressions;
using SmartX.API.Data;
using SmartX.API.Models;

namespace SmartX.API.Services
{
    //Checks a sensor registration and returns every problem found (empty list = valid)
    public static class SensorValidator
    {
        // Letters, digits, dot, dash, underscore. Starts with a letter or digit. 3-50 characters.
        // No slashes or spaces, so the identifier is safe in URLs, file names and log lines.
        private static readonly Regex IdentifierPattern =
            new(@"^[A-Za-z0-9][A-Za-z0-9._-]{2,49}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

        public static List<string> Validate(Sensor? sensor)
        {
            var errors = new List<string>();

            if (sensor == null)
            {
                errors.Add("Sensor details are required.");
                return errors;
            }

            var id = sensor.DeviceIdentifier?.Trim() ?? string.Empty;

            if (id.Length == 0)
                errors.Add("Device identifier is required.");
            else if (!IdentifierPattern.IsMatch(id))
                errors.Add("Device identifier must be 3-50 characters: letters, digits, '.', '-' or '_' (for example ESP32-001).");
            else if (string.Equals(id, MeshTopologyService.GatewayId, StringComparison.OrdinalIgnoreCase))
                errors.Add($"'{MeshTopologyService.GatewayId}' is reserved for the gateway.");

            CheckText(errors, sensor.DeploymentLocation, "Deployment location");
            CheckText(errors, sensor.Zone, "Sensor zone");

            if (!Enum.IsDefined(sensor.Category))
                errors.Add("Invalid sensor category.");

            if (!string.IsNullOrWhiteSpace(sensor.MacAddress))
            {
                if (!SmartXDataStore.IsValidMac(sensor.MacAddress))
                {
                    errors.Add("MAC address must look like 24:6F:28:AB:CD:EF (six pairs of hex digits).");
                }
                else
                {
                    var mac = SmartXDataStore.NormalizeMac(sensor.MacAddress);
                    int firstByte = Convert.ToInt32(mac[..2], 16);

                    if (mac == "00:00:00:00:00:00" || mac == "FF:FF:FF:FF:FF:FF")
                        errors.Add("MAC address cannot be all zeros or the broadcast address.");
                    else if ((firstByte & 1) == 1)
                        errors.Add("MAC address must be a unicast address (first byte must be even).");
                }
            }

            return errors;
        }

        private static void CheckText(List<string> errors, string? value, string name)
        {
            var text = value?.Trim() ?? string.Empty;

            if (text.Length == 0)
                errors.Add($"{name} is required.");
            else if (text.Length > 100)
                errors.Add($"{name} must be 100 characters or fewer.");
            else if (text.Any(char.IsControl))
                errors.Add($"{name} contains invalid characters.");
        }
    }
}
