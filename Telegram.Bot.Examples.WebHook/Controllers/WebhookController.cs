namespace Telegram.Bot.Examples.WebHook.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Services;
    using Types;

    public class WebhookController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Post([FromServices] HandleUpdateService handleUpdateService,
            [FromBody] Update update)
        {
            await handleUpdateService.EchoAsync(update: update);
            return this.Ok();
        }
    }
}
