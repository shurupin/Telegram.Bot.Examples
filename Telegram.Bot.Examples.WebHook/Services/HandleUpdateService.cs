namespace Telegram.Bot.Examples.WebHook.Services
{
    using Exceptions;
    using Types;
    using Types.Enums;
    using Types.InlineQueryResults;
    using Types.InputFiles;
    using Types.ReplyMarkups;

    using YoutubeExplode;
    using YoutubeExplode.Videos;
    using YoutubeExplode.Videos.Streams;

    public class HandleUpdateService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ILogger<HandleUpdateService> _logger;

        public HandleUpdateService(ITelegramBotClient botClient, ILogger<HandleUpdateService> logger)
        {
            this._botClient = botClient;
            this._logger = logger;
        }

        public async Task EchoAsync(Update update)
        {
            Task handler = update.Type switch
            {
                // UpdateType.Unknown:
                // UpdateType.ChannelPost:
                // UpdateType.EditedChannelPost:
                // UpdateType.ShippingQuery:
                // UpdateType.PreCheckoutQuery:
                // UpdateType.Poll:
                UpdateType.Message            => this.BotOnMessageReceived(message: update.Message!),
                UpdateType.EditedMessage      => this.BotOnMessageReceived(message: update.EditedMessage!),
                UpdateType.CallbackQuery      => this.BotOnCallbackQueryReceived(callbackQuery: update.CallbackQuery!),
                UpdateType.InlineQuery        => this.BotOnInlineQueryReceived(inlineQuery: update.InlineQuery!),
                UpdateType.ChosenInlineResult => this.BotOnChosenInlineResultReceived(chosenInlineResult: update.ChosenInlineResult!),
                _                             => this.UnknownUpdateHandlerAsync(update: update)
            };

            try
            {
                await handler;
            }
            catch (Exception exception)
            {
                await this.HandleErrorAsync(exception: exception);
            }
        }

        private async Task BotOnMessageReceived(Message message)
        {
            this._logger.LogInformation(message: "Receive message type: {messageType}", message.Type);
            if (message.Type != MessageType.Text)
                return;

            string text = message.Text!.Split(separator: ' ')[0];
            Task<Message> action = text switch
            {
                "/inline" => SendInlineKeyboard(bot: this._botClient, message: message),
                "/keyboard" => SendReplyKeyboard(bot: this._botClient, message: message),
                "/remove" => RemoveKeyboard(bot: this._botClient, message: message),
                "/photo" => SendFile(bot: this._botClient, message: message),
                "/request" => RequestContactAndLocation(bot: this._botClient, message: message),
                { } value when value.Contains(value: "youtu") => DownloadAndSendVideo(bot: this._botClient, message: message),
                _ => Usage(bot: this._botClient, message: message)
            };

            Message sentMessage = await action;
            this._logger.LogInformation(message: "The message was sent with id: {sentMessageId}",sentMessage.MessageId);

            // Send inline keyboard
            // You can process responses in BotOnCallbackQueryReceived handler
            static async Task<Message> SendInlineKeyboard(ITelegramBotClient bot, Message message)
            {
                await bot.SendChatActionAsync(chatId: message.Chat.Id, chatAction: ChatAction.Typing);

                // Simulate longer running task
                await Task.Delay(millisecondsDelay: 500);

                InlineKeyboardMarkup inlineKeyboard = new(
                    inlineKeyboard: new[]
                    {
                        // first row
                        new []
                        {
                            InlineKeyboardButton.WithCallbackData(text: "1.1", callbackData: "11"),
                            InlineKeyboardButton.WithCallbackData(text: "1.2", callbackData: "12"),
                        },
                        // second row
                        new []
                        {
                            InlineKeyboardButton.WithCallbackData(text: "2.1", callbackData: "21"),
                            InlineKeyboardButton.WithCallbackData(text: "2.2", callbackData: "22"),
                        },
                    });

                return await bot.SendTextMessageAsync(chatId: message.Chat.Id,
                    text: "Choose",
                    replyMarkup: inlineKeyboard);
            }

            static async Task<Message> SendReplyKeyboard(ITelegramBotClient bot, Message message)
            {
                ReplyKeyboardMarkup replyKeyboardMarkup = new(
                    keyboard: new[]
                    {
                        new KeyboardButton[] { "1.1", "1.2" },
                        new KeyboardButton[] { "2.1", "2.2" },
                    })
                {
                    ResizeKeyboard = true
                };

                return await bot.SendTextMessageAsync(chatId: message.Chat.Id,
                    text: "Choose",
                    replyMarkup: replyKeyboardMarkup);
            }

            static async Task<Message> RemoveKeyboard(ITelegramBotClient bot, Message message)
            {
                return await bot.SendTextMessageAsync(chatId: message.Chat.Id,
                    text: "Removing keyboard",
                    replyMarkup: new ReplyKeyboardRemove());
            }

            static async Task<Message> SendFile(ITelegramBotClient bot, Message message)
            {
                await bot.SendChatActionAsync(chatId: message.Chat.Id, chatAction: ChatAction.UploadPhoto);

                const string filePath = @"Files/tux.png";
                using FileStream fileStream = new(path: filePath, mode: FileMode.Open, access: FileAccess.Read, share: FileShare.Read);
                string fileName = filePath.Split(separator: Path.DirectorySeparatorChar).Last();

                return await bot.SendPhotoAsync(chatId: message.Chat.Id,
                    photo: new InputOnlineFile(content: fileStream, fileName: fileName),
                    caption: "Nice Picture");
            }

            static async Task<Message> RequestContactAndLocation(ITelegramBotClient bot, Message message)
            {
                ReplyKeyboardMarkup RequestReplyKeyboard = new(
                    keyboardRow: new[]
                    {
                        KeyboardButton.WithRequestLocation(text: "Location"),
                        KeyboardButton.WithRequestContact(text: "Contact"),
                    });

                return await bot.SendTextMessageAsync(chatId: message.Chat.Id,
                    text: "Who or Where are you?",
                    replyMarkup: RequestReplyKeyboard);
            }

            static async Task<Message> DownloadAndSendVideo(ITelegramBotClient bot, Message message)
            {
                await bot.SendChatActionAsync(chatId: message.Chat.Id, chatAction: ChatAction.UploadVideo);
                YoutubeClient youtube = new YoutubeClient();
                string videoId = VideoId.Parse(videoIdOrUrl: message.Text!.Split(separator: ' ')[0]);
                StreamManifest streamManifest = await youtube.Videos.Streams.GetManifestAsync(videoId: videoId);
                IVideoStreamInfo streamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();
                string filePath = @$"Files/video.{streamInfo.Container}";
                await youtube.Videos.Streams.DownloadAsync(streamInfo: streamInfo, filePath: filePath);
                await using FileStream fileStream = new(path: filePath, mode: FileMode.Open, access: FileAccess.Read, share: FileShare.Read);
                string fileName = filePath.Split(separator: Path.DirectorySeparatorChar).Last();
                return await bot.SendVideoAsync(chatId: message.Chat.Id, video: new InputOnlineFile(content: fileStream, fileName: fileName), caption: "Nice Video");
            }

            static async Task<Message> Usage(ITelegramBotClient bot, Message message)
            {
                const string usage = "Usage:\n" +
                    "/inline   - send inline keyboard\n" +
                    "/keyboard - send custom keyboard\n" +
                    "/remove   - remove custom keyboard\n" +
                    "/photo    - send a photo\n" +
                    "/request  - request location or contact";

                return await bot.SendTextMessageAsync(chatId: message.Chat.Id,
                    text: usage,
                    replyMarkup: new ReplyKeyboardRemove());
            }
        }

        // Process Inline Keyboard callback data
        private async Task BotOnCallbackQueryReceived(CallbackQuery callbackQuery)
        {
            await this._botClient.AnswerCallbackQueryAsync(
                callbackQueryId: callbackQuery.Id,
                text: $"Received {callbackQuery.Data}");

            await this._botClient.SendTextMessageAsync(
                chatId: callbackQuery.Message.Chat.Id,
                text: $"Received {callbackQuery.Data}");
        }

        #region Inline Mode

        private async Task BotOnInlineQueryReceived(InlineQuery inlineQuery)
        {
            this._logger.LogInformation(message: "Received inline query from: {inlineQueryFromId}", inlineQuery.From.Id);

            InlineQueryResult[] results = {
                // displayed result
                new InlineQueryResultArticle(
                    id: "3",
                    title: "TgBots",
                    inputMessageContent: new InputTextMessageContent(
                        messageText: "hello"
                    )
                )
            };

            await this._botClient.AnswerInlineQueryAsync(inlineQueryId: inlineQuery.Id,
                results: results,
                isPersonal: true,
                cacheTime: 0);
        }

        private Task BotOnChosenInlineResultReceived(ChosenInlineResult chosenInlineResult)
        {
            this._logger.LogInformation(message: "Received inline result: {chosenInlineResultId}", chosenInlineResult.ResultId);
            return Task.CompletedTask;
        }

        #endregion

        private Task UnknownUpdateHandlerAsync(Update update)
        {
            this._logger.LogInformation(message: "Unknown update type: {updateType}", update.Type);
            return Task.CompletedTask;
        }

        public Task HandleErrorAsync(Exception exception)
        {
            string ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            this._logger.LogInformation(message: "HandleError: {ErrorMessage}", ErrorMessage);
            return Task.CompletedTask;
        }
    }
}
