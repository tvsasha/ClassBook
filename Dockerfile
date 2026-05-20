FROM node:22-alpine AS client-build
WORKDIR /src/ClientApp
COPY ClientApp/package*.json ./
RUN npm ci
COPY ClientApp/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ClassBook.csproj ./
RUN dotnet restore ClassBook.csproj
COPY . ./
COPY --from=client-build /src/wwwroot/app ./wwwroot/app
RUN dotnet publish ClassBook.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
RUN mkdir -p /app/App_Data/DataProtectionKeys
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ClassBook.dll"]
