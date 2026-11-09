# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["Webhook_ListenerDotNet/Webhook_ListenerDotNet.csproj", "Webhook_ListenerDotNet/"]
RUN dotnet restore "Webhook_ListenerDotNet/Webhook_ListenerDotNet.csproj"

# Copy source and publish
COPY . .
WORKDIR "/src/Webhook_ListenerDotNet"
RUN dotnet publish "Webhook_ListenerDotNet.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Webhook_ListenerDotNet.dll"]

