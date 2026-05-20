FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/AGXIOMA.slnx .
COPY src/AGXIOME.Shared/ AGXIOME.Shared/
COPY src/AGXIOME.Interfaces/ AGXIOME.Interfaces/
COPY src/AGXIOME.LLMWIKY/ AGXIOME.LLMWIKY/
COPY src/AGXIOME.Core/ AGXIOME.Core/
COPY src/AGXIOME.Storage/ AGXIOME.Storage/
COPY src/AGXIOME.Runtime/ AGXIOME.Runtime/
COPY src/AGXIOME.Realtime/ AGXIOME.Realtime/
COPY src/AGXIOME.Security/ AGXIOME.Security/
COPY src/AGXIOME.Telemetry/ AGXIOME.Telemetry/
COPY src/AGXIOME.Toolkits/ AGXIOME.Toolkits/
COPY apps/agxioma-legal-email/ apps/agxioma-legal-email/
COPY data/ data/

RUN dotnet restore apps/agxioma-legal-email/AgxiomaLegalEmail.csproj
RUN dotnet publish apps/agxioma-legal-email/AgxiomaLegalEmail.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "AgxiomaLegalEmail.dll"]
