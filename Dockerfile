# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
#dossier du code source
WORKDIR /src
# Copy only project file first (better caching)
COPY GestionProjet.csproj .
RUN dotnet restore
# Copy the rest of the source code
COPY . .
RUN dotnet publish -c Release -o out 

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
#dossier de l'application compilée
WORKDIR /app
COPY --from=build /src/out .

#Pour garantir que ton API écoute bien dans le container.
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080 
ENTRYPOINT ["dotnet", "GestionProjet.dll"]