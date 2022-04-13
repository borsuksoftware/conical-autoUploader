using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BorsukSoftware.Conical.AutomaticUploader.Services
{
    public class TelegramService
    {
        public Microsoft.Extensions.Options.IOptions<TelegramOptions> TelegramOptions { get; private set; }

        public TelegramService(Microsoft.Extensions.Options.IOptions<TelegramOptions> telegramOptions)
        {
            this.TelegramOptions = telegramOptions;
        }

        public async Task<bool> SendMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            Console.WriteLine($"Telegram Service: {message}");

            var httpClient = new System.Net.Http.HttpClient()
            {
                BaseAddress = new Uri( "https://api.telegram.org" )
            };

            var url = $"bot{TelegramOptions.Value.BotKey}/sendMessage?chat_id={TelegramOptions.Value.ChatID}&text={System.Net.WebUtility.UrlEncode(message)}";
            var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);

            var response = await httpClient.SendAsync(request);
            switch (response.StatusCode)
            {
                case System.Net.HttpStatusCode.OK:
                    return true;

                default:
                    return false;
            }
        }
    }
}
