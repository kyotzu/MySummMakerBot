using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MySummMakerBot
{
    internal class Bot : BackgroundService
    {
        private ITelegramBotClient _telegramClient;
        private Dictionary<long, string> userStates = new(); // Хранит выбранные пользователем действия

        public Bot(ITelegramBotClient telegramClient)
        {
            _telegramClient = telegramClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _telegramClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                new ReceiverOptions() { AllowedUpdates = { } }, // Получаем все обновления
                cancellationToken: stoppingToken);

            Console.WriteLine("Бот запущен");
        }

        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message && update.Message!.Type == MessageType.Text)
            {
                var chatId = update.Message.From.Id;
                var messageText = update.Message.Text;

                if (messageText == "/start")
                {
                    await ShowMainMenuAsync(chatId, cancellationToken);
                    return;
                }

                if (userStates.ContainsKey(chatId))
                {
                    var userState = userStates[chatId];

                    if (userState == "count_chars")
                    {
                        // Подсчитываем количество символов
                        var length = messageText.Length;
                        await _telegramClient.SendTextMessageAsync(chatId, $"В вашем сообщении {length} символов.", cancellationToken: cancellationToken);
                    }
                    else if (userState == "sum_numbers")
                    {
                        // Пробуем вычислить сумму чисел
                        var numbers = messageText.Split(' ').Select(s => int.TryParse(s, out var n) ? n : (int?)null);

                        if (numbers.Any(n => n == null))
                        {
                            await _telegramClient.SendTextMessageAsync(chatId, "Пожалуйста, отправьте числа, разделённые пробелом.", cancellationToken: cancellationToken);
                        }
                        else
                        {
                            var sum = numbers.Sum(n => n ?? 0);
                            await _telegramClient.SendTextMessageAsync(chatId, $"Сумма чисел: {sum}", cancellationToken: cancellationToken);
                        }
                    }

                    return;
                }
            }

            // Обрабатываем нажатие на кнопку
            if (update.Type == UpdateType.CallbackQuery)
            {
                var chatId = update.CallbackQuery.From.Id;
                var action = update.CallbackQuery.Data;

                if (action == "count_chars")
                {
                    userStates[chatId] = "count_chars";
                    await _telegramClient.SendTextMessageAsync(chatId, "Введите текст, чтобы подсчитать количество символов:", cancellationToken: cancellationToken);
                }
                else if (action == "sum_numbers")
                {
                    userStates[chatId] = "sum_numbers";
                    await _telegramClient.SendTextMessageAsync(chatId, "Введите числа через пробел для вычисления суммы:", cancellationToken: cancellationToken);
                }

                return;
            }
        }

        // Главное меню с выбором действия
        private async Task ShowMainMenuAsync(long chatId, CancellationToken cancellationToken)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new [] // Первая строка кнопок
                {
                    InlineKeyboardButton.WithCallbackData("Подсчитать символы", "count_chars"),
                    InlineKeyboardButton.WithCallbackData("Сложить числа", "sum_numbers")
                }
            });

            await _telegramClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Выберите действие:",
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken
            );
        }

        Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(errorMessage);

            // Задержка перед повторным подключением
            Console.WriteLine("Ожидаем 10 секунд перед повторным подключением.");
            Thread.Sleep(10000);

            return Task.CompletedTask;
        }
    }
}
