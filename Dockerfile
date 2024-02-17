#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:3.1 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src
COPY ["KMedoids.csproj", "."]

RUN dotnet restore "KMedoids.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "KMedoids.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "KMedoids.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY out.csv /app/out.csv
RUN mkdir -p /app/data && chmod  777 /app/data

#ENTRYPOINT ["dotnet", "KMedoids.dll"]
