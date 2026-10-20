# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID

# Build the API and its referenced projects
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files first so Docker can restore dependencies
COPY ["ApexApp.Api/ApexApp.Api.csproj", "ApexApp.Api/"]
COPY ["ApexApp.Infrastructure/ApexApp.Infrastructure.csproj", "ApexApp.Infrastructure/"]
COPY ["ApexApp.Domain/ApexApp.Domain.csproj", "ApexApp.Domain/"]

RUN dotnet restore "ApexApp.Api/ApexApp.Api.csproj"

# Copy the rest of the repository
COPY . .

WORKDIR /src/ApexApp.Api
RUN dotnet publish "ApexApp.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

# Final runtime image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ApexApp.Api.dll"]
