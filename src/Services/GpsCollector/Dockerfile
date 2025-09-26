FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/Services/GpsCollector/GpsCollector.csproj", "src/Services/GpsCollector/"]
COPY ["src/Shared/Shared.Messaging.RabbitMqClient/Shared.Messaging.RabbitMqClient.csproj", "src/Shared/Shared.Messaging.RabbitMqClient/"]
COPY ["src/Shared/Shared/Shared.csproj", "src/Shared/Shared/"]
COPY ["src/Shared/Shared.Contracts/Shared.Contracts.csproj", "src/Shared/Shared.Contracts/"]

RUN dotnet restore "./src/Services/GpsCollector/GpsCollector.csproj"
COPY . .
WORKDIR "/src/src/Services/GpsCollector"
RUN dotnet build "./GpsCollector.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GpsCollector.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GpsCollector.dll"]
