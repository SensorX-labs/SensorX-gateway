# ─────────────────────────────────────────────────────────────────
#  SensorX Gateway — Multi-stage Dockerfile
#  Build:  docker build -t sensorx-gateway .
# ─────────────────────────────────────────────────────────────────

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore — copy csproj files first for layer caching
COPY src/SensorX.Gateway.Domain/SensorX.Gateway.Domain.csproj             SensorX.Gateway.Domain/
COPY src/SensorX.Gateway.Application/SensorX.Gateway.Application.csproj   SensorX.Gateway.Application/
COPY src/SensorX.Gateway.Infrastructure/SensorX.Gateway.Infrastructure.csproj SensorX.Gateway.Infrastructure/
COPY src/SensorX.Gateway.Api/SensorX.Gateway.Api.csproj                   SensorX.Gateway.Api/
RUN dotnet restore "SensorX.Gateway.Api/SensorX.Gateway.Api.csproj"

# Copy source and publish
COPY src/ .
RUN dotnet publish "SensorX.Gateway.Api/SensorX.Gateway.Api.csproj" \
    -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

COPY --from=build /app/publish .

# Create keys dir as root, then hand over ownership
RUN mkdir -p /app/Keys && chown -R appuser:appgroup /app/Keys

USER appuser


EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "SensorX.Gateway.Api.dll"]
