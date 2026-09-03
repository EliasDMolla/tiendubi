FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Admin.WebApi/Admin.WebApi.csproj", "Admin.WebApi/"]
COPY ["Admin.Entities/Admin.Entities.csproj", "Admin.Entities/"]
RUN dotnet restore "Admin.WebApi/Admin.WebApi.csproj"

COPY . .
WORKDIR "/src/Admin.WebApi"
RUN dotnet publish "Admin.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

RUN apt-get update \
	&& apt-get install -y --no-install-recommends fonts-dejavu-core fonts-liberation \
	&& rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8081
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=8081

EXPOSE 8081

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Admin.WebApi.dll"]