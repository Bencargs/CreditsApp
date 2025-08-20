#FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
#WORKDIR /app
#EXPOSE 80
#EXPOSE 443
#
#FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
#WORKDIR /src
#COPY ["CreditsApp.csproj", "./"]
#RUN dotnet restore "CreditsApp.csproj"
#COPY . .
#WORKDIR "/src/"
#RUN dotnet build "CreditsApp.csproj" -c Release -o /app/build
#
#FROM build AS publish
#RUN dotnet publish "CreditsApp.csproj" -c Release -o /app/publish
#
#FROM base AS final
#WORKDIR /app
#COPY --from=publish /app/publish .
#ENTRYPOINT ["dotnet", "CreditsApp.dll"]

# build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "Credits.Api.csproj"
RUN dotnet publish "Credits.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Credits.Api.dll"]