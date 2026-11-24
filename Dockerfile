FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/DevBench.Api/DevBench.Api.csproj src/DevBench.Api/packages.lock.json src/DevBench.Api/
RUN dotnet restore src/DevBench.Api/DevBench.Api.csproj --locked-mode
COPY src/ src/
RUN dotnet publish src/DevBench.Api/DevBench.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "DevBench.Api.dll"]
