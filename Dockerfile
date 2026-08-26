# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Aqevryn/Aqevryn.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish src/Aqevryn/Aqevryn.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
RUN groupadd -r aqevryn && useradd -r -g aqevryn -d /app -s /bin/bash aqevryn
RUN chown -R aqevryn:aqevryn /app
USER aqevryn
ENTRYPOINT ["dotnet", "Aqevryn.dll"]