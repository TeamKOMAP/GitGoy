using System.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Vcs.Domain.Entities;
using Vcs.Infrastructure.Data;
using Vcs.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=storage/vcs.db";
var useSqlite = connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
    || connectionString.EndsWith(".db", StringComparison.OrdinalIgnoreCase);

if (useSqlite)
{
    var dataSource = connectionString
        .Split(';', StringSplitOptions.RemoveEmptyEntries)
        .FirstOrDefault(part => part.TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase));
    var dbPath = dataSource?[(dataSource.IndexOf('=') + 1)..].Trim();
    var dbDirectory = string.IsNullOrWhiteSpace(dbPath) ? null : Path.GetDirectoryName(dbPath);
    if (!string.IsNullOrWhiteSpace(dbDirectory))
    {
        Directory.CreateDirectory(dbDirectory);
    }
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useSqlite)
    {
        options.UseSqlite(connectionString);
        return;
    }

    options.UseSqlServer(connectionString);
});

builder.Services.AddScoped<ProjectService>();
builder.Services.AddSingleton<GitService>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine("storage", "keys")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Vcs API", Version = "v1" });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (useSqlite)
    {
        db.Database.EnsureCreated();
    }
    else
    {
        db.Database.Migrate();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();

if (app.Configuration.GetValue<bool>("WebUi:OpenBrowserOnStart"))
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
        var address = addresses?.FirstOrDefault(item => item.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            ?? addresses?.FirstOrDefault()
            ?? "http://localhost:5221";
        var url = NormalizeBrowserUrl(address);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Could not open web UI in a browser.");
        }
    });
}

app.Run();

static string NormalizeBrowserUrl(string address)
{
    var url = address
        .Replace("0.0.0.0", "localhost", StringComparison.OrdinalIgnoreCase)
        .Replace("[::]", "localhost", StringComparison.OrdinalIgnoreCase)
        .TrimEnd('/');

    return $"{url}/";
}
