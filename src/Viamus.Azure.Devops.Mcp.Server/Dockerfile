# See https://aka.ms/customizecontainer to learn how to customize your debug container

# Base stage - ASP.NET runtime for web applications
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Viamus.Azure.Devops.Mcp.Server/Viamus.Azure.Devops.Mcp.Server.csproj", "src/Viamus.Azure.Devops.Mcp.Server/"]
RUN dotnet restore "./src/Viamus.Azure.Devops.Mcp.Server/Viamus.Azure.Devops.Mcp.Server.csproj"
COPY . .
WORKDIR "/src/src/Viamus.Azure.Devops.Mcp.Server"
RUN dotnet build "./Viamus.Azure.Devops.Mcp.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Viamus.Azure.Devops.Mcp.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:PublishSingleFile=false /p:SelfContained=false

# Final stage
FROM base AS final
#Install curl for health checks
USER root
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
USER $APP_UID

WORKDIR /app
COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Viamus.Azure.Devops.Mcp.Server.dll"]
