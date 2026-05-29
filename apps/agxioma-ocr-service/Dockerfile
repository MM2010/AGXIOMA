FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY apps/agxioma-ocr-service/ ./apps/agxioma-ocr-service/

RUN dotnet restore apps/agxioma-ocr-service/AgxiomaOcrService.csproj
RUN dotnet publish apps/agxioma-ocr-service/AgxiomaOcrService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
        tesseract-ocr \
        tesseract-ocr-eng \
        tesseract-ocr-ita \
    && rm -rf /var/lib/apt/lists/*

EXPOSE 8080

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Ocr__DefaultLanguage=eng
ENV Ocr__MaxFileBytes=10485760
ENV TESSDATA_PREFIX=/usr/share/tesseract-ocr/5/tessdata/

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "AgxiomaOcrService.dll"]