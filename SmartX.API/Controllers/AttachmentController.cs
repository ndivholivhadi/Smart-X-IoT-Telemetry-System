using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using SmartX.API.Services;

namespace SmartX.API.Controllers
{
    [ApiController]
    [Route("api/sensors/{sensorId}/attachments")]
    public class AttachmentController : ControllerBase
    {
        private readonly AttachmentService _attachmentService;

        public AttachmentController(AttachmentService attachmentService)
        {
            _attachmentService = attachmentService;
        }

        [HttpPost]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> Upload(Guid sensorId, IFormFile file)
        {
            try
            {
                var attachment = await _attachmentService
                        .SaveAttachmentAsync(sensorId, file);

                return Created($"/api/sensors/{sensorId}/attachments/{attachment.Id}", attachment);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }

        // GET api/sensors/{sensorId}/attachments/{attachmentId}/download
        [HttpGet("{attachmentId:guid}/download")]
        public async Task<IActionResult> Download(Guid sensorId, Guid attachmentId)
        {
            try
            {
                var download = await _attachmentService.ReadAttachmentAsync(sensorId, attachmentId);

                if (download == null)
                    return NotFound(new { message = "Attachment not found." });

                // The browser must not guess a type and run the content.
                Response.Headers["X-Content-Type-Options"] = "nosniff";

                return File(download.Content, download.ContentType, download.FileName);
            }
            catch (CryptographicException)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "The stored file failed its integrity check, so it was not returned."
                });
            }
        }

        [HttpGet]
        public IActionResult GetAttachments(Guid sensorId)
        {
            var attachments = _attachmentService
                    .GetSensorAttachments(sensorId);

            return Ok(attachments);
        }
    }
}