FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG TARGETARCH
WORKDIR /code
COPY --link . .
WORKDIR /code/runner/
RUN dotnet publish
WORKDIR /code/KodeRunnerLibs/
RUN dotnet publish
WORKDIR /code
RUN mkdir -p runner/bin/Release/net8.0/publish/koderunner/Runnables
RUN cp KodeRunnerLibs/Runnables/bin/Release/net8.0/publish/Runnables.dll runner/bin/Release/net8.0/publish/koderunner/Runnables/Runnables.dll
WORKDIR /code/runner/bin/Release/net8.0/publish/
RUN tar cvzf /app.tar.gz *
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine
COPY --from=build /app.tar.gz /app.tar.gz
RUN  mkdir /KodeRunner/
WORKDIR /KodeRunner/
RUN tar xzvf /app.tar.gz && rm /app.tar.gz
RUN ls -l && ./KodeRunner --init 
CMD ["/KodeRunner/KodeRunner"]
