# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy solution and project files first for layer caching
COPY PlanToSave.sln .
COPY src/PlanToSave.Domain/PlanToSave.Domain.csproj       src/PlanToSave.Domain/
COPY src/PlanToSave.Application/PlanToSave.Application.csproj src/PlanToSave.Application/
COPY src/PlanToSave.Infrastructure/PlanToSave.Infrastructure.csproj src/PlanToSave.Infrastructure/
COPY src/PlanToSave.Web/PlanToSave.Web.csproj             src/PlanToSave.Web/

RUN dotnet restore src/PlanToSave.Web/PlanToSave.Web.csproj

# Copy remaining source and publish
COPY src/ src/
RUN dotnet publish src/PlanToSave.Web/PlanToSave.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Runtime image ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Render always uses PORT=10000; hardcode it so no shell expansion is needed
ENV ASPNETCORE_URLS=http://+:10000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000

ENTRYPOINT ["dotnet", "PlanToSave.Web.dll"]
