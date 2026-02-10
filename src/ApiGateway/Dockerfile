FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/ApiGateway/TagAlong.ApiGateway.csproj", "src/ApiGateway/"]
RUN dotnet restore "src/ApiGateway/TagAlong.ApiGateway.csproj"
COPY . .
WORKDIR "/src/src/ApiGateway"
RUN dotnet build "TagAlong.ApiGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TagAlong.ApiGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TagAlong.ApiGateway.dll"]
