# Build context is the repo root (docker-compose sets it that way) so this stage can restore/publish
# ecomm.api without pulling in ecomm.web or ecomm.tests — this project has no sibling ProjectReferences.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ecomm.api/ecomm.api.csproj ./ecomm.api/
RUN dotnet restore ./ecomm.api/ecomm.api.csproj
COPY ecomm.api/. ./ecomm.api/
RUN dotnet publish ./ecomm.api/ecomm.api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# dp-keys and uploads are bind-mounted by docker-compose.yml onto host directories that must survive
# container recreation (see documents/v4-stages/phase-0-portability.md) - this image never bakes them in.
EXPOSE 5090
ENV ASPNETCORE_URLS=http://+:5090

ENTRYPOINT ["dotnet", "ecomm.api.dll"]
