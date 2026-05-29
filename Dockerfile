# AGXIOME — Multi-stage Dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/AGXIOMA.slnx .
COPY src/AGXIOME.Shared/ AGXIOME.Shared/
COPY src/AGXIOME.Interfaces/ AGXIOME.Interfaces/
COPY src/AGXIOME.LLMWIKY/ AGXIOME.LLMWIKY/
COPY src/AGXIOME.Core/ AGXIOME.Core/
COPY src/AGXIOME.Storage/ AGXIOME.Storage/
COPY src/AGXIOME.CLI/ AGXIOME.CLI/
COPY src/AGXIOME.Telemetry/ AGXIOME.Telemetry/
COPY src/AGXIOME.Security/ AGXIOME.Security/
COPY src/AGXIOME.Realtime/ AGXIOME.Realtime/
COPY src/AGXIOME.DynamicRuntime/ AGXIOME.DynamicRuntime/
COPY src/AGXIOME.Runtime/ AGXIOME.Runtime/
COPY src/AGXIOME.Orchestration/ AGXIOME.Orchestration/
COPY src/AGXIOME.Toolkits/ AGXIOME.Toolkits/
COPY src/AGXIOME.Embedders/ AGXIOME.Embedders/
COPY src/AGXIOME.Readers/ AGXIOME.Readers/
COPY src/AGXIOME.XLink/ AGXIOME.XLink/

RUN dotnet restore AGXIOME.CLI/AGXIOME.CLI.csproj
RUN dotnet publish AGXIOME.CLI/AGXIOME.CLI.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
    && rm -rf /var/lib/apt/lists/*

EXPOSE 8080
EXPOSE 8443

COPY --from=build /app/publish .

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "AGXIOME.CLI.dll"]
