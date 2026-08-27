FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Aqevryn/Aqevryn.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish src/Aqevryn/Aqevryn.csproj -c Release -o /app

# Copy config files for the runtime (both as example and default)
COPY sources.example.yaml /app/sources.example.yaml
RUN cp /app/sources.example.yaml /app/sources.yaml

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
RUN groupadd -r aqevryn && useradd -r -g aqevryn -d /app -s /bin/bash aqevryn
RUN chown -R aqevryn:aqevryn /app
USER aqevryn
ENTRYPOINT ["dotnet", "Aqevryn.dll"]