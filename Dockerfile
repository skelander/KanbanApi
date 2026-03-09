FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY KanbanApi/KanbanApi.csproj KanbanApi/
COPY KanbanApi/packages.lock.json KanbanApi/
RUN dotnet restore KanbanApi/KanbanApi.csproj --locked-mode
COPY KanbanApi/ KanbanApi/
RUN dotnet publish KanbanApi/KanbanApi.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "KanbanApi.dll"]
