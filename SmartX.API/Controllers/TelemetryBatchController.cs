using Microsoft.AspNetCore.Mvc;
using SmartX.API.Models;
using SmartX.API.Services;

namespace SmartX.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TelemetryBatchController : ControllerBase
    {
        private readonly TelemetryBatchService _batchService;

        public TelemetryBatchController(TelemetryBatchService batchService)
        {
            _batchService = batchService;
        }
        [HttpPost]
        public IActionResult ProcessBatch(TelemetryBatch batch)
        {
            if (batch.RawTelemetry == null || batch.RawTelemetry.Length == 0)
            {
                return BadRequest(new
                {
                    message = "Telemetry batch cannot be empty."
                });
            }

            var result = _batchService.ProcessBatch(batch);

            return Ok(new
            {
                message = "Telemetry batch processed successfully.",

                result.DeviceIdentifier,

                rawArrayType = "double[][] (jagged array)",

                optimizedCollectionType = "List<double>",

                result.NumberOfBatches,

                result.TotalRawValues,

                result.ValuesProcessed,

                result.BatchSizes
            });
        }
    }
}