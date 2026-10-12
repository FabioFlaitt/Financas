# Imagem de build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia apenas os projetos necessários ao publish da API (restaura só o que precisa)
COPY src/ src/
RUN dotnet restore src/FinanceApp.Api/FinanceApp.Api.csproj
RUN dotnet publish src/FinanceApp.Api/FinanceApp.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Imagem de runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FinanceApp.Api.dll"]
