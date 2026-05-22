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
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=5 \
    CMD bash -c 'exec 3<>/dev/tcp/localhost/8080 && printf "GET /health HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3 && head -1 <&3 | grep -q "200"'
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "AgxiomaLegalEmail.dll"]
