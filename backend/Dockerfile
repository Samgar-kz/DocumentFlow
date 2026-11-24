# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/DocumentFlow.Domain/DocumentFlow.Domain.csproj src/DocumentFlow.Domain/
COPY src/DocumentFlow.Application/DocumentFlow.Application.csproj src/DocumentFlow.Application/
COPY src/DocumentFlow.Infrastructure/DocumentFlow.Infrastructure.csproj src/DocumentFlow.Infrastructure/
COPY src/DocumentFlow.Api/DocumentFlow.Api.csproj src/DocumentFlow.Api/
RUN dotnet restore src/DocumentFlow.Api/DocumentFlow.Api.csproj

COPY src/ src/
RUN dotnet publish src/DocumentFlow.Api/DocumentFlow.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Storage__RootPath=/app/storage
EXPOSE 8080

# Uploaded files live in a volume mounted here; the non-root app user must own it.
RUN mkdir -p /app/storage && chown "$APP_UID" /app/storage
USER $APP_UID
COPY --from=build /app .
ENTRYPOINT ["dotnet", "DocumentFlow.Api.dll"]
