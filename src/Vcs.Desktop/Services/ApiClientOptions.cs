using System.IO;
using System.Text.Json;

namespace Vcs.Desktop.Services;

public sealed record ApiClientOptions
{
    public string BaseUrl { get; init; } = "http://localhost:5221";
    public string Username { get; init; } = "desktop-client";
    public string Password { get; init; } = "Password123!";

    public static ApiClientOptions Load()
    {
        var options = LoadFromFile();

        return options with
        {
            BaseUrl = ReadEnvironment("VCS_API_BASE_URL", options.BaseUrl),
            Username = ReadEnvironment("VCS_API_USERNAME", options.Username),
            Password = ReadEnvironment("VCS_API_PASSWORD", options.Password)
        };
    }

    private static ApiClientOptions LoadFromFile()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "clientsettings.json");
        if (!File.Exists(path))
        {
            return new ApiClientOptions();
        }

        using var stream = File.OpenRead(path);
        var settings = JsonSerializer.Deserialize<ClientSettings>(stream, JsonOptions);
        return settings?.Api ?? new ApiClientOptions();
    }

    private static string ReadEnvironment(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record ClientSettings(ApiClientOptions Api);
}
