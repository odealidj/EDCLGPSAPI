FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/Services/VendorMaster/VendorMaster.Api/VendorMaster.Api.csproj", "src/Services/VendorMaster/VendorMaster.Api/"]
COPY ["src/Services/VendorMaster/VendorMaster.Application/VendorMaster.Application.csproj", "src/Services/VendorMaster/VendorMaster.Application/"]
COPY ["src/Services/VendorMaster/VendorMaster.Domain/VendorMaster.Domain.csproj", "src/Services/VendorMaster/VendorMaster.Domain/"]
COPY ["src/Services/VendorMaster/VendorMaster.Infrastructure/VendorMaster.Infrastructure.csproj", "src/Services/VendorMaster/VendorMaster.Infrastructure/"]
COPY ["src/Shared/Shared/Shared.csproj", "src/Shared/Shared/"]
COPY ["src/Shared/Shared.Contracts/Shared.Contracts.csproj", "src/Shared/Shared.Contracts/"]

RUN dotnet restore "./src/Services/VendorMaster/VendorMaster.Api/VendorMaster.Api.csproj"
COPY . .
WORKDIR "/src/src/Services/VendorMaster/VendorMaster.Api"
RUN dotnet build "./VendorMaster.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./VendorMaster.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VendorMaster.Api.dll"]
