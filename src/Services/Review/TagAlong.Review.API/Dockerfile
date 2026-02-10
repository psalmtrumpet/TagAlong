FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/Review/TagAlong.Review.API/TagAlong.Review.API.csproj", "src/Services/Review/TagAlong.Review.API/"]
COPY ["src/Services/Review/TagAlong.Review.Domain/TagAlong.Review.Domain.csproj", "src/Services/Review/TagAlong.Review.Domain/"]
COPY ["src/Services/Review/TagAlong.Review.Infrastructure/TagAlong.Review.Infrastructure.csproj", "src/Services/Review/TagAlong.Review.Infrastructure/"]
COPY ["src/BuildingBlocks/Common/TagAlong.Common/TagAlong.Common.csproj", "src/BuildingBlocks/Common/TagAlong.Common/"]
COPY ["src/BuildingBlocks/Common/TagAlong.Common.Contracts/TagAlong.Common.Contracts.csproj", "src/BuildingBlocks/Common/TagAlong.Common.Contracts/"]
COPY ["src/BuildingBlocks/EventBus/TagAlong.EventBus/TagAlong.EventBus.csproj", "src/BuildingBlocks/EventBus/TagAlong.EventBus/"]
COPY ["src/BuildingBlocks/EventBus/TagAlong.EventBus.RabbitMQ/TagAlong.EventBus.RabbitMQ.csproj", "src/BuildingBlocks/EventBus/TagAlong.EventBus.RabbitMQ/"]
RUN dotnet restore "src/Services/Review/TagAlong.Review.API/TagAlong.Review.API.csproj"
COPY . .
WORKDIR "/src/src/Services/Review/TagAlong.Review.API"
RUN dotnet build "TagAlong.Review.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TagAlong.Review.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TagAlong.Review.API.dll"]
