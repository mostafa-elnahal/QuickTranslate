Write-Host "Building Light (Framework-Dependent) version..."
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishDir=bin\Release\net8.0-windows\win-x64\publish_light

Write-Host "`nBuilding Standalone (Self-Contained) version..."
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishDir=bin\Release\net8.0-windows\win-x64\publish_standalone

$isccPath = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $isccPath)) {
    $isccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
}

Write-Host "`nCompiling Light Installer..."
& $isccPath /DIsSelfContained=0 QuickTranslate.iss

Write-Host "`nCompiling Standalone Installer..."
& $isccPath /DIsSelfContained=1 QuickTranslate.iss

Write-Host "`nDone! Installers are in the SetupOutput directory."
