FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/IntegrationWatch.Core/IntegrationWatch.Core.csproj", "src/IntegrationWatch.Core/"]
COPY ["src/IntegrationWatch.Application/IntegrationWatch.Application.csproj", "src/IntegrationWatch.Application/"]
COPY ["src/IntegrationWatch.Infrastructure/IntegrationWatch.Infrastructure.csproj", "src/IntegrationWatch.Infrastructure/"]
COPY ["src/IntegrationWatch.Api/IntegrationWatch.Api.csproj", "src/IntegrationWatch.Api/"]
COPY ["IntegrationWatch.slnx", "./"]

RUN dotnet restore "src/IntegrationWatch.Api/IntegrationWatch.Api.csproj"

COPY . .
WORKDIR "/src/src/IntegrationWatch.Api"
RUN dotnet publish "IntegrationWatch.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5050
ENV ASPNETCORE_URLS=http://+:5050
ENTRYPOINT ["dotnet", "IntegrationWatch.Api.dll"]
