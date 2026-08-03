FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY FinanceApp.Api/FinanceApp.Api.csproj FinanceApp.Api/
RUN dotnet restore FinanceApp.Api/FinanceApp.Api.csproj

COPY FinanceApp.Api/ FinanceApp.Api/
RUN dotnet publish FinanceApp.Api/FinanceApp.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:10000
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
EXPOSE 10000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FinanceApp.Api.dll"]
