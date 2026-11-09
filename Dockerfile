# syntax=docker/dockerfile:1

# ---------- build stage: full SDK, compiles and publishes the app ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project file first and restore. Docker caches this layer, so rebuilds after a code
# change skip the slow NuGet restore unless the .csproj itself changed.
COPY RaceDay.Web.csproj ./
RUN dotnet restore RaceDay.Web.csproj

COPY . .
RUN dotnet publish RaceDay.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- runtime stage: small ASP.NET image, no SDK, runs as a non-root user ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# The pages decide "upcoming" and "past" with DateTime.Now. Containers default to UTC, so the time zone is
# set to the one the event organisers and the local run use, so both show the same events as "past".
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    TZ=Africa/Johannesburg \
    DOTNET_gcServer=0 \
    DOTNET_TieredPGO=1

EXPOSE 8080
COPY --from=build /app/publish ./

# The base image ships an unprivileged "app" user; APP_UID is its id.
USER $APP_UID

# Where to find the RaceDay API is supplied at run time: -e ApiBaseUrl=http://host.docker.internal:5146/
ENTRYPOINT ["dotnet", "RaceDay.Web.dll"]
