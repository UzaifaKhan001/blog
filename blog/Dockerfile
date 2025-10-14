# --- Build Stage ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the project file first (to leverage caching)
COPY blog.csproj ./

# Restore dependencies (this layer is cached unless csproj changes)
RUN dotnet restore blog.csproj

# Copy the rest of the app
COPY . ./

# Publish the application
RUN dotnet publish blog.csproj -c Release -o /app/publish --no-restore

# --- Runtime Stage ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published output from build stage
COPY --from=build /app/publish .

# Expose port
EXPOSE 80

ENTRYPOINT ["dotnet", "blog.dll"]
