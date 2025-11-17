# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["Quotations.Api/Quotations.Api.csproj", "Quotations.Api/"]
COPY ["Quotations.Tests/Quotations.Tests.csproj", "Quotations.Tests/"]
RUN dotnet restore "Quotations.Api/Quotations.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/Quotations.Api"
RUN dotnet build "Quotations.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Quotations.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

EXPOSE 5000
EXPOSE 5001

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Quotations.Api.dll"]