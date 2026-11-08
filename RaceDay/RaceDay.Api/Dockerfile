# ---------- Build stage: compile and publish the API ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project file first, so the (slow) package restore is cached until the packages change
COPY RaceDay.Api/RaceDay.Api.csproj RaceDay.Api/
RUN dotnet restore RaceDay.Api/RaceDay.Api.csproj

# Then copy the rest of the code and publish
COPY RaceDay.Api/ RaceDay.Api/
RUN dotnet publish RaceDay.Api/RaceDay.Api.csproj -c Release -o /app/publish --no-restore

# ---------- Runtime stage: a small image with only what is needed to run ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# The .NET images listen on port 8080 by default
EXPOSE 8080

# Run as the non-root user that comes with the .NET images
USER $APP_UID

ENTRYPOINT ["dotnet", "RaceDay.Api.dll"]
