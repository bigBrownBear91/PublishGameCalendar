FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["PublishGameCalendar/PublishGameCalendar.csproj", "PublishGameCalendar/"]
RUN dotnet restore "PublishGameCalendar/PublishGameCalendar.csproj"
COPY PublishGameCalendar/ PublishGameCalendar/
RUN dotnet publish "PublishGameCalendar/PublishGameCalendar.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PublishGameCalendar.dll"]
