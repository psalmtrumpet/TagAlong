FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/Configuration/TagAlong.Configuration.API/TagAlong.Configuration.API.csproj", "src/Services/Configuration/TagAlong.Configuration.API/"]
COPY ["src/Services/Configuration/TagAlong.Configuration.Domain/TagAlong.Configuration.Domain.csproj", "src/Services/Configuration/TagAlong.Configuration.Domain/"]
COPY ["src/Services/Configuration/TagAlong.Configuration.Infrastructure/TagAlong.Configuration.Infrastructure.csproj", "src/Services/Configuration/TagAlong.Configuration.Infrastructure/"]
COPY ["src/BuildingBlocks/Common/TagAlong.Common/TagAlong.Common.csproj", "src/BuildingBlocks/Common/TagAlong.Common/"]
COPY ["src/BuildingBlocks/Common/TagAlong.Common.Contracts/TagAlong.Common.Contracts.csproj", "src/BuildingBlocks/Common/TagAlong.Common.Contracts/"]
COPY ["src/BuildingBlocks/EventBus/TagAlong.EventBus/TagAlong.EventBus.csproj", "src/BuildingBlocks/EventBus/TagAlong.EventBus/"]
COPY ["src/BuildingBlocks/EventBus/TagAlong.EventBus.RabbitMQ/TagAlong.EventBus.RabbitMQ.csproj", "src/BuildingBlocks/EventBus/TagAlong.EventBus.RabbitMQ/"]
RUN dotnet restore "src/Services/Configuration/TagAlong.Configuration.API/TagAlong.Configuration.API.csproj"
COPY . .
WORKDIR "/src/src/Services/Configuration/TagAlong.Configuration.API"
RUN dotnet build "TagAlong.Configuration.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TagAlong.Configuration.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TagAlong.Configuration.API.dll"]
