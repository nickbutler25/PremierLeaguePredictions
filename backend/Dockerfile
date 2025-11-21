# Use the official .NET SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["PremierLeaguePredictions.API/PremierLeaguePredictions.API.csproj", "PremierLeaguePredictions.API/"]
COPY ["PremierLeaguePredictions.Application/PremierLeaguePredictions.Application.csproj", "PremierLeaguePredictions.Application/"]
COPY ["PremierLeaguePredictions.Core/PremierLeaguePredictions.Core.csproj", "PremierLeaguePredictions.Core/"]
COPY ["PremierLeaguePredictions.Infrastructure/PremierLeaguePredictions.Infrastructure.csproj", "PremierLeaguePredictions.Infrastructure/"]

RUN dotnet restore "PremierLeaguePredictions.API/PremierLeaguePredictions.API.csproj"

# Copy the rest of the code
COPY . .

# Build and publish the application
WORKDIR "/src/PremierLeaguePredictions.API"
RUN dotnet publish "PremierLeaguePredictions.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Use the runtime image for running the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Expose the port that Render will use
EXPOSE 8080

# Copy the published app
COPY --from=build /app/publish .

# Set environment variable for ASP.NET to listen on all interfaces
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "PremierLeaguePredictions.API.dll"]
