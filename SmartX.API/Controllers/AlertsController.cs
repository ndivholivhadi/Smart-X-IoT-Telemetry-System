using Microsoft.AspNetCore.Mvc;
using SmartX.API.Services;

namespace SmartX.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AlertsController : ControllerBase
    {
        private readonly AnomalyDetectionService _anomalyDetectionService;

        public AlertsController(AnomalyDetectionService anomalyDetectionService)
        {
            _anomalyDetectionService = anomalyDetectionService;
        }

        [HttpGet]
        public IActionResult GetAlerts()
        {
            return Ok(_anomalyDetectionService.GetAlerts());
        }

        [HttpGet("recent")]
        public IActionResult GetRecentAlerts()
        {
            return Ok(_anomalyDetectionService.GetRecentAlerts());
        }

        [HttpGet("next-priority")]
        public IActionResult GetNextPriorityAlert()
        {
            var alert = _anomalyDetectionService.GetNextPriorityAlert();

            if (alert == null)
            {
                return NoContent();
            }

            return Ok(alert);
        }

        [HttpGet("priority")]
        public IActionResult GetPriorityAlerts()
        {
            return Ok(_anomalyDetectionService.GetPriorityAlerts());
        }

        [HttpGet("tracking")]
        public IActionResult GetTracking()
        {
            return Ok(new
            {
                activeErrors = _anomalyDetectionService.GetActiveErrors(),
                disconnectedDevices = _anomalyDetectionService.GetDisconnectedDevices()
            });
        }

    }
}