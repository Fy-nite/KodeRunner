cd runner
dotnet publish
cd ../
cd KodeRunnerLibs/Runnables
dotnet build
cd ../../
cd runner
# Initialize the runner  to make sure that we have the runnables folder
dotnet run --init
# Copy the runnables to the runner folder

New-Item -ItemType Directory -Path koderunner/Runnables
cp ../KodeRunnerLibs/Runnables/bin/Debug/net8.0/Runnables.dll koderunner/Runnables/Runnables.dll