FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY DevBench.Api/DevBench.Api.csproj DevBench.Api/packages.lock.json DevBench.Api/
RUN dotnet restore DevBench.Api/DevBench.Api.csproj --locked-mode
COPY DevBench.Api/ DevBench.Api/
RUN dotnet publish DevBench.Api/DevBench.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "DevBench.Api.dll"]
