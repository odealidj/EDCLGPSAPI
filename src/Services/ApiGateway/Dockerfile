# Stage 1: Build & Native AOT Compilation
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release

# Install Native AOT build prerequisites (clang & zlib)
RUN apt-get update && apt-get install -y --no-install-recommends \
    clang \
    zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

# Copy project files for caching restore layer
COPY ["src/Services/ApiGateway/ApiGateway.csproj", "src/Services/ApiGateway/"]
COPY ["src/Shared/Shared/Shared.csproj", "src/Shared/Shared/"]
COPY ["src/Shared/Shared.Contracts/Shared.Contracts.csproj", "src/Shared/Shared.Contracts/"]
RUN dotnet restore "./src/Services/ApiGateway/ApiGateway.csproj" -r linux-x64

# Copy source code and publish Native AOT binary
COPY . .
WORKDIR "/src/src/Services/ApiGateway"
RUN dotnet publish "./ApiGateway.csproj" -c $BUILD_CONFIGURATION -r linux-x64 -o /app/publish

# Stage 2: Final ultra-slim distroless runtime container (Ubuntu Noble Chiseled)
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["./ApiGateway"]
