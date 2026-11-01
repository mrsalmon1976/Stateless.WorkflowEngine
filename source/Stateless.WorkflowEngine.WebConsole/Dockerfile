# Builds the web console image.  The build context must be the "source" folder, e.g. from the repo root:
#   docker build -f source/Stateless.WorkflowEngine.WebConsole/Dockerfile -t stateless-workflowengine-webconsole source

# --- client ---
FROM node:22-alpine AS client
WORKDIR /src/Stateless.WorkflowEngine.WebConsole.Client
COPY Stateless.WorkflowEngine.WebConsole.Client/package.json Stateless.WorkflowEngine.WebConsole.Client/package-lock.json ./
RUN npm ci
COPY Stateless.WorkflowEngine.WebConsole.Client/ ./
RUN npm run build

# --- server ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Stateless.WorkflowEngine/ Stateless.WorkflowEngine/
COPY Stateless.WorkflowEngine.MongoDb/ Stateless.WorkflowEngine.MongoDb/
COPY Stateless.WorkflowEngine.RavenDb/ Stateless.WorkflowEngine.RavenDb/
COPY Stateless.WorkflowEngine.WebConsole/ Stateless.WorkflowEngine.WebConsole/
COPY --from=client /src/Stateless.WorkflowEngine.WebConsole/wwwroot/ Stateless.WorkflowEngine.WebConsole/wwwroot/
RUN dotnet publish Stateless.WorkflowEngine.WebConsole/Stateless.WorkflowEngine.WebConsole.csproj -c Release -o /app/publish -p:SkipClientBuild=true

# --- runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV Urls=http://+:8080 \
    DataPath=/app/Data
EXPOSE 8080
# users, connections and data protection keys - mount a volume here so they survive container upgrades
VOLUME /app/Data
ENTRYPOINT ["dotnet", "Stateless.WorkflowEngine.WebConsole.dll"]
