using Microsoft.AspNetCore.Mvc;
using SmartX.API.Models;

namespace SmartX.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TelemetryCalculationController : ControllerBase
    {

        [HttpPost("aggregate")]
        public IActionResult Aggregate([FromBody] TelemetryCalculationRequest request)
        {
            var result = request.First + request.Second;

            return Ok(new
            {
                operation = "Operator Overloading (+)",
                description = "The overloaded + operator was used to aggregate two telemetry readings.",

                firstDevice = request.First.DeviceIdentifier,
                firstValue = request.First.Value,

                secondDevice = request.Second.DeviceIdentifier,
                secondValue = request.Second.Value,

                calculation = $"{request.First.Value} + {request.Second.Value}",

                result = result.Value,
                unit = result.Unit,
                timestamp = result.Timestamp
            });
        }

        [HttpPost("difference")]
        public IActionResult Difference([FromBody] TelemetryCalculationRequest request)
        {
            var result = request.First - request.Second;

            return Ok(new
            {
                operation = "Operator Overloading (-)",
                description = "The overloaded - operator was used to calculate the difference between two telemetry readings.",

                firstDevice = request.First.DeviceIdentifier,
                firstValue = request.First.Value,

                secondDevice = request.Second.DeviceIdentifier,
                secondValue = request.Second.Value,

                calculation = $"{request.First.Value} - {request.Second.Value}",

                result = result.Value,
                unit = result.Unit,
                timestamp = result.Timestamp
            });
        }
    }
}