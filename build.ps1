Push-Location -Path (Join-Path $PSScriptRoot '/magika')
dotnet restore
dotnet clean

# Build and publish for class library
## for Windows (Legacy .NET Framework)
dotnet publish -c Release -f netstandard2.0

## for Windows (.NET Core)
dotnet publish -c Release -f net6.0 -r win-x64
dotnet publish -c Release -f net6.0 -r win-arm64

## for Linux (.NET Core)
dotnet publish -c Release -f net6.0 -r linux-x64
dotnet publish -c Release -f net6.0 -r linux-arm64

## for macOS (.NET Core)
dotnet publish -c Release -f net6.0 -r osx-x64
dotnet publish -c Release -f net6.0 -r osx-arm64

## NuGet package
dotnet pack -c Release


# Build and publish for console application
Push-Location "./cli"
dotnet restore
dotnet clean

## for Windows
dotnet publish -c Release -f net8.0 -r win-x64
dotnet publish -c Release -f net8.0 -r win-arm64

## for Linux
dotnet publish -c Release -f net8.0 -r linux-x64
dotnet publish -c Release -f net8.0 -r linux-arm64

## for macOS
dotnet publish -c Release -f net8.0 -r osx-x64
dotnet publish -c Release -f net8.0 -r osx-arm64

Pop-Location
Pop-Location
