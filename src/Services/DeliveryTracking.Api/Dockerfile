FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/Services/DeliveryTracking.Api/DeliveryTracking.Api.csproj", "src/Services/DeliveryTracking.Api/"]
COPY ["src/Shared/Shared/Shared.csproj", "src/Shared/Shared/"]
COPY ["src/Shared/Shared.Contracts/Shared.Contracts.csproj", "src/Shared/Shared.Contracts/"]

RUN dotnet restore "./src/Services/DeliveryTracking.Api/DeliveryTracking.Api.csproj"
COPY . .
WORKDIR "/src/src/Services/DeliveryTracking.Api"
RUN dotnet build "./DeliveryTracking.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DeliveryTracking.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DeliveryTracking.Api.dll"]
