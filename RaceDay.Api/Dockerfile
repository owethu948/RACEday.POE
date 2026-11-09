# syntax=docker/dockerfile:1
# Optional: lets `docker compose up` run the whole stack (SQL Server + API + web).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY RaceDay.Api.csproj ./
RUN dotnet restore RaceDay.Api.csproj
COPY . .
RUN dotnet publish RaceDay.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish ./
USER $APP_UID
ENTRYPOINT ["dotnet", "RaceDay.Api.dll"]
