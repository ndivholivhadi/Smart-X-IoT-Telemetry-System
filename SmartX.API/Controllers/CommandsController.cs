
using Microsoft.AspNetCore.Mvc;
using SmartX.API.Models;
using SmartX.API.Services;

namespace SmartX.API.Controllers
{
    [ApiController]
    [Route("api/commands")]
    public class CommandsController : ControllerBase
    {
        private readonly CommandService _commandService;

        public CommandsController(CommandService commandService)
        {
            _commandService = commandService;
        }

        public class SubmitCommandRequest
        {
            public string DeviceIdentifier { get; set; } = string.Empty;
            public string CommandType { get; set; } = string.Empty;
            public string? Description { get; set; }
        }

        [HttpPost]
        public IActionResult SubmitCommand(
            [FromBody] SubmitCommandRequest request)
        {
            try
            {
                var command = _commandService.SubmitCommand(
                    request.DeviceIdentifier,
                    request.CommandType,
                    request.Description);

                return Accepted(command);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("execute-next")]
        public IActionResult ExecuteNextCommand()
        {
            var command = _commandService.ExecuteNextCommand();

            if (command == null)
            {
                return NoContent();
            }

            return Ok(command);
        }

        [HttpGet("history")]
        public IActionResult GetHistory()
        {
            return Ok(_commandService.GetHistory());
        }

        [HttpGet("auto-process")]
        public IActionResult GetAutoProcess()
        {
            return Ok(new { enabled = _commandService.AutoProcess });
        }

        [HttpPost("auto-process")]
        public IActionResult SetAutoProcess([FromQuery] bool enabled)
        {
            _commandService.AutoProcess = enabled;
            return Ok(new { enabled = _commandService.AutoProcess });
        }

        [HttpGet("pending")]
        public IActionResult GetPendingCommands()
        {
            return Ok(_commandService.GetPendingCommands());
        }

        [HttpPost("undo")]
        public IActionResult UndoLastCommand()
        {
            var command = _commandService.UndoLastCommand();

            if (command == null)
            {
                return Conflict(new
                {
                    message = "There are no commands available to undo."
                });
            }

            if (!command.IsUndone)
            {
                return Conflict(command);
            }

            return Ok(command);
        }
    }
}
