FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/User/TagAlong.User.API/TagAlong.User.API.csproj", "src/Services/User/TagAlong.User.API/"]
COPY ["src/Services/User/TagAlong.User.Domain/TagAlong.User.Domain.csproj", "src/Services/User/TagAlong.User.Domain/"]
COPY ["src/Services/User/TagAlong.User.Infrastructure/TagAlong.User.Infrastructure.csproj", "src/Services/User/TagAlong.User.Infrastructure/"]
COPY ["src/BuildingBlocks/Common/TagAlong.Common/TagAlong.Common.csproj", "src/BuildingBlocks/Common/TagAlong.Common/"]
COPY ["src/BuildingBlocks/Common/TagAlong.Common.Contracts/TagAlong.Common.Contracts.csproj", "src/BuildingBlocks/Common/TagAlong.Common.Contracts/"]
COPY ["src/BuildingBlocks/EventBus/TagAlong.EventBus/TagAlong.EventBus.csproj", "src/BuildingBlocks/EventBus/TagAlong.EventBus/"]
COPY ["src/BuildingBlocks/EventBus/TagAlong.EventBus.RabbitMQ/TagAlong.EventBus.RabbitMQ.csproj", "src/BuildingBlocks/EventBus/TagAlong.EventBus.RabbitMQ/"]
RUN dotnet restore "src/Services/User/TagAlong.User.API/TagAlong.User.API.csproj"
COPY . .
WORKDIR "/src/src/Services/User/TagAlong.User.API"
RUN dotnet build "TagAlong.User.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TagAlong.User.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TagAlong.User.API.dll"]
