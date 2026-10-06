using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PromptQuest.Web.Services.Storage.Db;
using PromptQuest.Web.Services.Storage.InMemory;

namespace PromptQuest.Web.Services.Storage;

// Выбор реализации IPlayerStore/IAttemptStore/ILeaderboardService вынесен из
// Program.cs в отдельный метод, чтобы его можно было проверить тестом напрямую
// на IServiceCollection, без запуска хоста целиком (top-level statements в
// Program.cs читают конфигурацию синхронно до WebApplicationFactory успевает
// подставить тестовую — метод ниже обходит эту проблему).
public static class StorageRegistration
{
    public static void AddStorage(
        this IServiceCollection services, bool useInMemoryStorage, string? connectionString)
    {
        if (useInMemoryStorage)
        {
            services.AddSingleton<InMemoryPlayerStore>();
            services.AddSingleton<IPlayerStore>(sp => sp.GetRequiredService<InMemoryPlayerStore>());
            services.AddSingleton<IAttemptStore, InMemoryAttemptStore>();
            services.AddSingleton<ILeaderboardService, InMemoryLeaderboardService>();
            return;
        }

        const string connectionError = "Connection string 'Default' must provide non-empty Host, Database and Username. "
            + "Configure ConnectionStrings__Default before starting the application.";

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(connectionError);
        }

        NpgsqlConnectionStringBuilder connection;
        try
        {
            connection = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            // Do not include the connection string or parser exception: they may contain credentials.
            throw new InvalidOperationException(connectionError);
        }

        if (string.IsNullOrWhiteSpace(connection.Host)
            || string.IsNullOrWhiteSpace(connection.Database)
            || string.IsNullOrWhiteSpace(connection.Username))
        {
            throw new InvalidOperationException(connectionError);
        }

        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<IPlayerStore, EfPlayerStore>();
        services.AddScoped<IAttemptStore, EfAttemptStore>();
        services.AddScoped<ILeaderboardService, EfLeaderboardService>();
    }
}
