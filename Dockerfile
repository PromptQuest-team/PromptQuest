FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY PromptQuest.Web/PromptQuest.Web.csproj PromptQuest.Web/
RUN dotnet restore PromptQuest.Web/PromptQuest.Web.csproj
COPY PromptQuest.Web/ PromptQuest.Web/
RUN dotnet publish PromptQuest.Web/PromptQuest.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000
# Render передаёт порт в переменной PORT (по умолчанию 10000)
CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-10000} exec dotnet PromptQuest.Web.dll"]
