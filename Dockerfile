# Builds the API with the .NET 8 SDK, then runs it on the smaller ASP.NET Core runtime image.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Restore from the project files alone first, so the package layer is reused until a project file changes.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/GameOfLife.Core/GameOfLife.Core.csproj src/GameOfLife.Core/
COPY src/GameOfLife.Api/GameOfLife.Api.csproj src/GameOfLife.Api/
RUN dotnet restore src/GameOfLife.Api/GameOfLife.Api.csproj

COPY src/ src/
RUN dotnet publish src/GameOfLife.Api/GameOfLife.Api.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# Run as the image's built-in non-root user. ASP.NET Core 8 images listen on port 8080.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "GameOfLife.Api.dll"]
