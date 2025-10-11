using Entities.Model;
using Microsoft.AspNetCore.Mvc;
using OutboxApi.Services;

namespace OutboxApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OutboxController : ControllerBase
    {
        private readonly IOutboxService _outboxService;

        public OutboxController(IOutboxService outboxService)
        {
            _outboxService = outboxService;
        }

        [HttpPost]
        public async Task<IActionResult> AddNewOutbox([FromBody] Outbox outbox)
        {
            if (outbox == null)
                return BadRequest("Outbox message cannot be null");

            await _outboxService.AddNewOutboxAsync(outbox);
            return Ok();
        }

        [HttpGet("unprocessed")]
        public async Task<ActionResult<List<Outbox>>> GetUnprocessedOutboxes()
        {
            var outboxes = await _outboxService.GetUnprocessedOutboxesAsync();
            return Ok(outboxes);
        }

        [HttpPut("{id}/processed")]
        public async Task<IActionResult> MarkAsProcessed(Guid id)
        {
            await _outboxService.MarkOutboxAsProcessedAsync(id);
            return NoContent();
        }

        [HttpPut("{id}/failed")]
        public async Task<IActionResult> MarkAsFailed(Guid id)
        {
            await _outboxService.MarkOutboxAsFailedAsync(id);
            return NoContent();
        }
    }
}
