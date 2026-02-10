FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/Report/TagAlong.Report.API/TagAlong.Report.API.csproj", "src/Services/Report/TagAlong.Report.API/"]
COPY ["src/Services/Report/TagAlong.Report.Domain/TagAlong.Report.Domain.csproj", "src/Services/Report/TagAlong.Report.Domain/"]
COPY ["src/Services/Report/TagAlong.Report.Infrastructure/TagAlong.Report.Infrastructure.csproj", "src/Services/Report/TagAlong.Report.Infrastructure/"]
COPY ["src/BuildingBlocks/Common/TagAlong.Common/TagAlong.Common.csproj", "src/BuildingBlocks/Common/TagAlong.Common/"]
COPY ["src/BuildingBlocks/Common/TagAlong.Common.Contracts/TagAlong.Common.Contracts.csproj", "src/BuildingBlocks/Common/TagAlong.Common.Contracts/"]
COPY ["src/BuildingBlocks/EventBus/TagAlong.EventBus/TagAlong.EventBus.csproj", "src/BuildingBlocks/EventBus/TagAlong.EventBus/"]
COPY ["src/BuildingBlocks/EventBus/TagAlong.EventBus.RabbitMQ/TagAlong.EventBus.RabbitMQ.csproj", "src/BuildingBlocks/EventBus/TagAlong.EventBus.RabbitMQ/"]
RUN dotnet restore "src/Services/Report/TagAlong.Report.API/TagAlong.Report.API.csproj"
COPY . .
WORKDIR "/src/src/Services/Report/TagAlong.Report.API"
RUN dotnet build "TagAlong.Report.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TagAlong.Report.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TagAlong.Report.API.dll"]
