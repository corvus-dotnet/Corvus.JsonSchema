# The checkpoint-listener runtime image: the framework-dependent published app on the ASP.NET Core runtime base. The app
# is published (dotnet publish) before this image is built, so the build only stages the output — no restore or build
# happens here, and the build context is the publish directory. It listens on 8080 (the Container App target port).
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY . ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Corvus.Text.Json.Arazzo.ServerlessCheckpointListener.dll"]
