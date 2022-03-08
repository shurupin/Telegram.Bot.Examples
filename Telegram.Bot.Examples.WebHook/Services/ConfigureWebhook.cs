namespace Telegram.Bot.Examples.WebHook.Services
{
    using Types.Enums;

    public class ConfigureWebhook : IHostedService
    {
        private readonly ILogger<ConfigureWebhook> _logger;
        private readonly IServiceProvider _services;
        private readonly BotConfiguration _botConfig;

        public ConfigureWebhook(ILogger<ConfigureWebhook> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            this._logger = logger;
            this._services = serviceProvider;
            this._botConfig = configuration.GetSection(key: "BotConfiguration").Get<BotConfiguration>();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using IServiceScope scope = this._services.CreateScope();
            ITelegramBotClient botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

            // Configure custom endpoint per Telegram API recommendations:
            // https://core.telegram.org/bots/api#setwebhook
            // If you'd like to make sure that the Webhook request comes from Telegram, we recommend
            // using a secret path in the URL, e.g. https://www.example.com/<token>.
            // Since nobody else knows your bot's token, you can be pretty sure it's us.
            string webhookAddress = @$"{this._botConfig.HostAddress}/bot/{this._botConfig.BotToken}";
            this._logger.LogInformation(message: "Setting webhook: {webhookAddress}", webhookAddress);
            await botClient.SetWebhookAsync(
                url: webhookAddress,
                allowedUpdates: Array.Empty<UpdateType>(),
                cancellationToken: cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            using IServiceScope scope = this._services.CreateScope();
            ITelegramBotClient botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

            // Remove webhook upon app shutdown
            this._logger.LogInformation(message: "Removing webhook");
            await botClient.DeleteWebhookAsync(cancellationToken: cancellationToken);
        }
    }
}
