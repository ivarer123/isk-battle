# One image for the API and the scheduled jobs (ARCHITECTURE.md §8).
#   API (default):     dotnet /app/api/IskBattle.Api.dll
#   Reminder job:      dotnet /app/jobs/IskBattle.Jobs.dll reminders   (Container Apps Job command override)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the layer is cached until a project or package version changes.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/IskBattle.Domain/IskBattle.Domain.csproj src/IskBattle.Domain/
COPY src/IskBattle.Infrastructure/IskBattle.Infrastructure.csproj src/IskBattle.Infrastructure/
COPY src/IskBattle.Api/IskBattle.Api.csproj src/IskBattle.Api/
COPY src/IskBattle.Jobs/IskBattle.Jobs.csproj src/IskBattle.Jobs/
RUN dotnet restore src/IskBattle.Api/IskBattle.Api.csproj \
 && dotnet restore src/IskBattle.Jobs/IskBattle.Jobs.csproj

COPY src/IskBattle.Domain/ src/IskBattle.Domain/
COPY src/IskBattle.Infrastructure/ src/IskBattle.Infrastructure/
COPY src/IskBattle.Api/ src/IskBattle.Api/
COPY src/IskBattle.Jobs/ src/IskBattle.Jobs/
# The OpenAPI document is generated and committed by normal builds; the web client isn't in this context.
RUN dotnet publish src/IskBattle.Api/IskBattle.Api.csproj -c Release --no-restore -o /out/api -p:OpenApiGenerateDocuments=false \
 && dotnet publish src/IskBattle.Jobs/IskBattle.Jobs.csproj -c Release --no-restore -o /out/jobs \
 && rm -f /out/api/appsettings.Development.json /out/jobs/appsettings.Development.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app/api
COPY --from=build /out/api /app/api
COPY --from=build /out/jobs /app/jobs
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "/app/api/IskBattle.Api.dll"]
