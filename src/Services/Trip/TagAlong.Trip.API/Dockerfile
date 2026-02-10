FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/Trip/TagAlong.Trip.API/TagAlong.Trip.API.csproj", "src/Services/Trip/TagAlong.Trip.API/"]
COPY ["src/Services/Trip/TagAlong.Trip.Domain/TagAlong.Trip.Domain.csproj", "src/Services/Trip/TagAlong.Trip.Domain/"]
COPY ["src/Services/Trip/TagAlong.Trip.Infrastructure/TagAlong.Trip.Infrastructure.csproj", "src/Services/Trip/TagAlong.Trip.Infrastructure/"]
COPY ["src/BuildingBlocks/Common/TagAlong.Common/TagAlong.Common.csproj", "src/BuildingBlocks/Common/TagAlong.Common/"]
COPY ["src/BuildingBlocks/Common/TagAlong.Common.Contracts/TagAlong.Common.Contracts.csproj", "src/BuildingBlocks/Common/TagAlong.Common.Contracts/"]
COPY ["src/BuildingBlocks/EventBus/TagAlong.EventBus/TagAlong.EventBus.csproj", "src/BuildingBlocks/EventBus/TagAlong.EventBus/"]
COPY ["src/BuildingBlocks/EventBus/TagAlong.EventBus.RabbitMQ/TagAlong.EventBus.RabbitMQ.csproj", "src/BuildingBlocks/EventBus/TagAlong.EventBus.RabbitMQ/"]
RUN dotnet restore "src/Services/Trip/TagAlong.Trip.API/TagAlong.Trip.API.csproj"
COPY . .
WORKDIR "/src/src/Services/Trip/TagAlong.Trip.API"
RUN dotnet build "TagAlong.Trip.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TagAlong.Trip.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TagAlong.Trip.API.dll"]
