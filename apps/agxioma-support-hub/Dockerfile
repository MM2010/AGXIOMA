FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/ ./src/
COPY apps/ ./apps/

RUN dotnet restore apps/agxioma-support-hub/AgxiomaSupportHub.csproj
RUN dotnet publish apps/agxioma-support-hub/AgxiomaSupportHub.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

EXPOSE 8080

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "AgxiomaSupportHub.dll"]