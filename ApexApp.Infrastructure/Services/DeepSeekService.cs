using System;
using System.Collections.Generic;
using System.Text;
using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services
{
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Text.Json;
    using Microsoft.Extensions.Options;

    public class DeepSeekService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly DeepSeekOptions _options;

        public DeepSeekService(
            HttpClient httpClient,
            IOptions<DeepSeekOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;

            _httpClient.BaseAddress =
                new Uri(_options.BaseUrl);

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _options.ApiKey);
        }

        public async Task<string> GenerateContentAsync(
            string prompt,
            CancellationToken cancellationToken = default)
        {
            return await SendRequestAsync(
                "Ты AI-ассистент корпоративной платформы ApexApp. " +
                "Помогай создавать качественные тексты для квестов, " +
                "заданий, мероприятий и наград. Отвечай на русском языке.",
                prompt,
                cancellationToken);
        }

        public async Task<string> AnalyzeEngagementAsync(
            string statistics,
            CancellationToken cancellationToken = default)
        {
            return await SendRequestAsync(
                "Ты аналитик вовлеченности сотрудников ApexApp. " +
                "Анализируй только предоставленную статистику. " +
                "Не выдумывай данные и не выдавай предположения за факты. " +
                "Предлагай конкретные активности и награды. " +
                "Отвечай на русском языке.",
                statistics,
                cancellationToken);
        }

        private async Task<string> SendRequestAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken)
        {
            var request = new
            {
                model = _options.Model,
                messages = new[]
                {
                new
                {
                    role = "system",
                    content = systemPrompt
                },
                new
                {
                    role = "user",
                    content = userPrompt
                }
            },
                stream = false,
                thinking = new
                {
                    type = "disabled"
                }
            };

            using var response = await _httpClient.PostAsJsonAsync(
                "chat/completions",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<
                DeepSeekResponse>(
                    cancellationToken: cancellationToken);

            return result?.Choices?.FirstOrDefault()
                ?.Message?.Content
                ?? throw new InvalidOperationException(
                    "DeepSeek вернул пустой ответ.");
        }
    }

    public class DeepSeekResponse
    {
        public List<DeepSeekChoice>? Choices { get; set; }
    }

    public class DeepSeekChoice
    {
        public DeepSeekMessage? Message { get; set; }
    }

    public class DeepSeekMessage
    {
        public string? Content { get; set; }
    }
}
