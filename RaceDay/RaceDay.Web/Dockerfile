# ---------- Build stage: compile and publish the website ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project file first, so the (slow) package restore is cached until the packages change
COPY RaceDay.Web/RaceDay.Web.csproj RaceDay.Web/
RUN dotnet restore RaceDay.Web/RaceDay.Web.csproj

# Then copy the rest of the code and publish
COPY RaceDay.Web/ RaceDay.Web/
RUN dotnet publish RaceDay.Web/RaceDay.Web.csproj -c Release -o /app/publish --no-restore

# ---------- Runtime stage: a small image with only what is needed to run ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# The .NET images listen on port 8080 by default
EXPOSE 8080

# Run as the non-root user that comes with the .NET images
USER $APP_UID

ENTRYPOINT ["dotnet", "RaceDay.Web.dll"]
