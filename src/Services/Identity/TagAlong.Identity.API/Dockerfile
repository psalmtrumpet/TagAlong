FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/Identity/TagAlong.Identity.API/TagAlong.Identity.API.csproj", "src/Services/Identity/TagAlong.Identity.API/"]
COPY ["src/Services/Identity/TagAlong.Identity.Domain/TagAlong.Identity.Domain.csproj", "src/Services/Identity/TagAlong.Identity.Domain/"]
COPY ["src/Services/Identity/TagAlong.Identity.Infrastructure/TagAlong.Identity.Infrastructure.csproj", "src/Services/Identity/TagAlong.Identity.Infrastructure/"]
COPY ["src/BuildingBlocks/Common/TagAlong.Common/TagAlong.Common.csproj", "src/BuildingBlocks/Common/TagAlong.Common/"]
COPY ["src/BuildingBlocks/Common/TagAlong.Common.Contracts/TagAlong.Common.Contracts.csproj", "src/BuildingBlocks/Common/TagAlong.Common.Contracts/"]
COPY ["src/BuildingBlocks/EventBus/TagAlong.EventBus/TagAlong.EventBus.csproj", "src/BuildingBlocks/EventBus/TagAlong.EventBus/"]
COPY ["src/BuildingBlocks/EventBus/TagAlong.EventBus.RabbitMQ/TagAlong.EventBus.RabbitMQ.csproj", "src/BuildingBlocks/EventBus/TagAlong.EventBus.RabbitMQ/"]
RUN dotnet restore "src/Services/Identity/TagAlong.Identity.API/TagAlong.Identity.API.csproj"
COPY . .
WORKDIR "/src/src/Services/Identity/TagAlong.Identity.API"
RUN dotnet build "TagAlong.Identity.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TagAlong.Identity.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TagAlong.Identity.API.dll"]
