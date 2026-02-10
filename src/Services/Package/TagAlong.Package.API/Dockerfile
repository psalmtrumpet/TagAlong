FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/Package/TagAlong.Package.API/TagAlong.Package.API.csproj", "src/Services/Package/TagAlong.Package.API/"]
COPY ["src/Services/Package/TagAlong.Package.Domain/TagAlong.Package.Domain.csproj", "src/Services/Package/TagAlong.Package.Domain/"]
COPY ["src/Services/Package/TagAlong.Package.Infrastructure/TagAlong.Package.Infrastructure.csproj", "src/Services/Package/TagAlong.Package.Infrastructure/"]
COPY ["src/BuildingBlocks/Common/TagAlong.Common/TagAlong.Common.csproj", "src/BuildingBlocks/Common/TagAlong.Common/"]
COPY ["src/BuildingBlocks/Common/TagAlong.Common.Contracts/TagAlong.Common.Contracts.csproj", "src/BuildingBlocks/Common/TagAlong.Common.Contracts/"]
COPY ["src/BuildingBlocks/EventBus/TagAlong.EventBus/TagAlong.EventBus.csproj", "src/BuildingBlocks/EventBus/TagAlong.EventBus/"]
COPY ["src/BuildingBlocks/EventBus/TagAlong.EventBus.RabbitMQ/TagAlong.EventBus.RabbitMQ.csproj", "src/BuildingBlocks/EventBus/TagAlong.EventBus.RabbitMQ/"]
RUN dotnet restore "src/Services/Package/TagAlong.Package.API/TagAlong.Package.API.csproj"
COPY . .
WORKDIR "/src/src/Services/Package/TagAlong.Package.API"
RUN dotnet build "TagAlong.Package.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TagAlong.Package.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TagAlong.Package.API.dll"]
