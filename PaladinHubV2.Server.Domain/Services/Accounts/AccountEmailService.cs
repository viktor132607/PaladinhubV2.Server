using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace PaladinHubV2.Server.Domain.Services.Accounts;

public sealed class AccountEmailService(HttpClient client, IConfiguration configuration)
{
    public async Task SendAsync(string address, string subject, string html)
    {
        string? key = configuration["Resend:ApiKey"];
        string? from = configuration["Resend:FromEmail"];
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("Resend settings are not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = JsonContent.Create(new {
            from = $"{configuration["Resend:FromName"] ?? "PaladinHub"} <{from}>",
            to = new[] { address }, subject, html
        });
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Email delivery failed. Please try again later.");
    }
}
