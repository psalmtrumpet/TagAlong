FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/Messaging/TagAlong.Messaging.API/TagAlong.Messaging.API.csproj", "src/Services/Messaging/TagAlong.Messaging.API/"]
COPY ["src/Services/Messaging/TagAlong.Messaging.Domain/TagAlong.Messaging.Domain.csproj", "src/Services/Messaging/TagAlong.Messaging.Domain/"]
COPY ["src/Services/Messaging/TagAlong.Messaging.Infrastructure/TagAlong.Messaging.Infrastructure.csproj", "src/Services/Messaging/TagAlong.Messaging.Infrastructure/"]
COPY ["src/BuildingBlocks/Common/TagAlong.Common/TagAlong.Common.csproj", "src/BuildingBlocks/Common/TagAlong.Common/"]
COPY ["src/BuildingBlocks/Common/TagAlong.Common.Contracts/TagAlong.Common.Contracts.csproj", "src/BuildingBlocks/Common/TagAlong.Common.Contracts/"]
COPY ["src/BuildingBlocks/EventBus/TagAlong.EventBus/TagAlong.EventBus.csproj", "src/BuildingBlocks/EventBus/TagAlong.EventBus/"]
COPY ["src/BuildingBlocks/EventBus/TagAlong.EventBus.RabbitMQ/TagAlong.EventBus.RabbitMQ.csproj", "src/BuildingBlocks/EventBus/TagAlong.EventBus.RabbitMQ/"]
RUN dotnet restore "src/Services/Messaging/TagAlong.Messaging.API/TagAlong.Messaging.API.csproj"
COPY . .
WORKDIR "/src/src/Services/Messaging/TagAlong.Messaging.API"
RUN dotnet build "TagAlong.Messaging.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TagAlong.Messaging.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TagAlong.Messaging.API.dll"]
