FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/Payment/TagAlong.Payment.API/TagAlong.Payment.API.csproj", "src/Services/Payment/TagAlong.Payment.API/"]
COPY ["src/Services/Payment/TagAlong.Payment.Domain/TagAlong.Payment.Domain.csproj", "src/Services/Payment/TagAlong.Payment.Domain/"]
COPY ["src/Services/Payment/TagAlong.Payment.Infrastructure/TagAlong.Payment.Infrastructure.csproj", "src/Services/Payment/TagAlong.Payment.Infrastructure/"]
COPY ["src/BuildingBlocks/Common/TagAlong.Common/TagAlong.Common.csproj", "src/BuildingBlocks/Common/TagAlong.Common/"]
COPY ["src/BuildingBlocks/Common/TagAlong.Common.Contracts/TagAlong.Common.Contracts.csproj", "src/BuildingBlocks/Common/TagAlong.Common.Contracts/"]
COPY ["src/BuildingBlocks/EventBus/TagAlong.EventBus/TagAlong.EventBus.csproj", "src/BuildingBlocks/EventBus/TagAlong.EventBus/"]
COPY ["src/BuildingBlocks/EventBus/TagAlong.EventBus.RabbitMQ/TagAlong.EventBus.RabbitMQ.csproj", "src/BuildingBlocks/EventBus/TagAlong.EventBus.RabbitMQ/"]
RUN dotnet restore "src/Services/Payment/TagAlong.Payment.API/TagAlong.Payment.API.csproj"
COPY . .
WORKDIR "/src/src/Services/Payment/TagAlong.Payment.API"
RUN dotnet build "TagAlong.Payment.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TagAlong.Payment.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TagAlong.Payment.API.dll"]
