FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY nuget.config .
COPY src/ src/
COPY apps/agxioma-legal-email/ apps/agxioma-legal-email/
COPY apps/agxioma-llm-wiki/ apps/agxioma-llm-wiki/
COPY data/ data/

RUN dotnet restore apps/agxioma-legal-email/AgxiomaLegalEmail.csproj
RUN dotnet publish apps/agxioma-legal-email/AgxiomaLegalEmail.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
COPY data/ /app/data/
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "AgxiomaLegalEmail.dll"]
