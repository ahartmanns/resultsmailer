#!/bin/sh
dotnet publish ResultsMailer.csproj -c Release -r win-x64 -o ./bin/Release/win-x64 /p:PublishSingleFile=true /p:PublishTrimmed=true
dotnet publish ResultsMailer.csproj -c Release -r linux-x64 -o ./bin/Release/linux-x64 /p:PublishSingleFile=true /p:PublishTrimmed=true
dotnet publish ResultsMailer.csproj -c Release -r osx-x64 -o ./bin/Release/osx-x64 /p:PublishSingleFile=true /p:PublishTrimmed=true
cd ./bin/Release
rm win-x64/*.pdb linux-x64/*.pdb osx-x64/*.pdb
zip -r9 Release.zip win-x64/ linux-x64/ osx-x64/
cd ../..
