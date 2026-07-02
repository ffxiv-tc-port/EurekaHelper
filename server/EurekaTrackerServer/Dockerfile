FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY EurekaTrackerServer.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV DataDirectory=/data
ENV ASPNETCORE_URLS=http://+:8080
VOLUME ["/data"]
EXPOSE 8080
ENTRYPOINT ["dotnet", "EurekaTrackerServer.dll"]
