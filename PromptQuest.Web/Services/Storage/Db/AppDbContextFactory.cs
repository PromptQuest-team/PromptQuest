using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PromptQuest.Web.Services.Storage.Db;

// Используется только инструментом dotnet-ef при генерации миграций
// (`dotnet ef migrations add`), в runtime-пути приложения не участвует.
// Program.cs регистрирует AppDbContext только в одной из двух веток конфигурации
// (БД включена и ConnectionStrings:Default задана) — это фабрика design-time,
// не зависящая от той ветки и от реального подключения, поэтому миграции можно
// генерировать без запуска приложения и без доступа к настоящей базе.
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=promptquest_designtime;Username=designtime");
        return new AppDbContext(optionsBuilder.Options);
    }
}
