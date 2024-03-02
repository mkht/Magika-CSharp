
$OutputDir = (Join-Path $PSScriptRoot '/Release')
Remove-Item $OutputDir -Recurse -Force -ErrorAction Ignore

Push-Location -Path (Join-Path $PSScriptRoot '/magika')

Remove-Item ./bin -Recurse -Force -ErrorAction Ignore
Remove-Item ./obj -Recurse -Force -ErrorAction Ignore

dotnet restore
dotnet clean

# Build and publish for class library
$name = 'magika-lib'

# win-x64
$arch = 'win-x64'
dotnet publish -c Release -f netstandard2.0 -r $arch -o ("$OutputDir/$name-$arch/netstandard2.0")
dotnet publish -c Release -f net6.0 -r $arch -o ("$OutputDir/$name-$arch/net6.0")

# win-arm64
$arch = 'win-arm64'
dotnet publish -c Release -f net6.0 -r $arch -o ("$OutputDir/$name-$arch/net6.0")

# linux-x64
$arch = 'linux-x64'
dotnet publish -c Release -f net6.0 -r $arch -o ("$OutputDir/$name-$arch/net6.0")
Remove-Item "$OutputDir/$name-$arch/net6.0/onnxruntime*.dll" -Force

# linux-arm64
$arch = 'linux-arm64'
dotnet publish -c Release -f net6.0 -r $arch -o ("$OutputDir/$name-$arch/net6.0")
Remove-Item "$OutputDir/$name-$arch/net6.0/onnxruntime*.dll" -Force

# osx-x64
$arch = 'osx-x64'
dotnet publish -c Release -f net6.0 -r $arch -o ("$OutputDir/$name-$arch/net6.0")
Remove-Item "$OutputDir/$name-$arch/net6.0/onnxruntime*.dll" -Force

# osx-arm64
$arch = 'osx-arm64'
dotnet publish -c Release -f net6.0 -r $arch -o ("$OutputDir/$name-$arch/net6.0")
Remove-Item "$OutputDir/$name-$arch/net6.0/onnxruntime*.dll" -Force

# NuGet package
dotnet pack -c Release -o $OutputDir


# Build and publish for console application
Push-Location "./cli"
Remove-Item ./bin -Recurse -Force -ErrorAction Ignore
Remove-Item ./obj -Recurse -Force -ErrorAction Ignore

dotnet restore
dotnet clean

$name = 'magika-cli'

# win-x64
$arch = 'win-x64'
dotnet publish -c Release -f net8.0 -r $arch -o ("$OutputDir/$name-$arch")
Remove-Item ("$OutputDir/$name-$arch/*") -Exclude "magika.exe" -Force -Recurse

# win-arm64
$arch = 'win-arm64'
dotnet publish -c Release -f net8.0 -r $arch -o ("$OutputDir/$name-$arch")
Remove-Item ("$OutputDir/$name-$arch/*") -Exclude "magika.exe" -Force -Recurse

# linux-x64
$arch = 'linux-x64'
dotnet publish -c Release -f net8.0 -r $arch -o ("$OutputDir/$name-$arch")
Remove-Item ("$OutputDir/$name-$arch/*") -Exclude "magika" -Force -Recurse

# linux-arm64
$arch = 'linux-arm64'
dotnet publish -c Release -f net8.0 -r $arch -o ("$OutputDir/$name-$arch")
Remove-Item ("$OutputDir/$name-$arch/*") -Exclude "magika" -Force -Recurse

# osx-x64
$arch = 'osx-x64'
dotnet publish -c Release -f net8.0 -r $arch -o ("$OutputDir/$name-$arch")
Remove-Item ("$OutputDir/$name-$arch/*") -Exclude "magika" -Force -Recurse

# osx-arm64
$arch = 'osx-arm64'
dotnet publish -c Release -f net8.0 -r $arch -o ("$OutputDir/$name-$arch")
Remove-Item ("$OutputDir/$name-$arch/*") -Exclude "magika" -Force -Recurse

Pop-Location
Pop-Location
