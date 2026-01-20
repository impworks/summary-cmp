FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY SummaryCmp.slnx .
COPY src/SummaryCmp.Web/SummaryCmp.Web.csproj src/SummaryCmp.Web/

# Restore dependencies
RUN dotnet restore SummaryCmp.slnx

# Copy all source files
COPY . .

# Build and publish
WORKDIR /src/src/SummaryCmp.Web
RUN dotnet publish -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Create data directory for SQLite
RUN mkdir -p /app/data

# Copy published app
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/summarycmp.db"

EXPOSE 8080

ENTRYPOINT ["dotnet", "SummaryCmp.Web.dll"]
