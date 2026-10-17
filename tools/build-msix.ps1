# Builds the Microsoft Store package (MSIX). The package is unsigned: the Store signs it on upload.
#
#   tools\build-msix.ps1 -Version 0.4.0 -IdentityName 12345Ack1.SoundDrought `
#       -Publisher "CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX" -PublisherDisplayName "Ack1"
#
# Identity values come from Partner Center > SoundDrought > Product management > Product identity.
param(
    [Parameter(Mandatory)] [string]$Version,
    [Parameter(Mandatory)] [string]$IdentityName,
    [Parameter(Mandatory)] [string]$Publisher,
    [Parameter(Mandatory)] [string]$PublisherDisplayName,
    [string]$OutDir = "out\msix"
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$layout = Join-Path $root "$OutDir\layout"
$package = Join-Path $root "$OutDir\SoundDrought-$Version.msix"

# The Store has no .NET 10 runtime package, so the Store build carries its own runtime.
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
dotnet publish (Join-Path $root 'src') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:Version=$Version -o $layout
if ($LASTEXITCODE) { throw "dotnet publish failed" }

Copy-Item (Join-Path $root 'packaging\msix\Assets') (Join-Path $layout 'Assets') -Recurse
$manifest = [IO.File]::ReadAllText((Join-Path $root 'packaging\msix\AppxManifest.xml'))
$manifest = $manifest.Replace('{Version}', "$Version.0").Replace('{IdentityName}', $IdentityName).
    Replace('{Publisher}', [Security.SecurityElement]::Escape($Publisher)).
    Replace('{PublisherDisplayName}', [Security.SecurityElement]::Escape($PublisherDisplayName))
[IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $manifest, (New-Object Text.UTF8Encoding $false))

# makeappx: Windows SDK if installed, otherwise the Microsoft.Windows.SDK.BuildTools NuGet package.
$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
    Where-Object FullName -match '\\x64\\' | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) {
    $nuget = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools'
    if (-not (Test-Path $nuget)) {
        $tmp = Join-Path ([IO.Path]::GetTempPath()) 'sounddrought-sdk-buildtools'
        New-Item -ItemType Directory -Force $tmp | Out-Null
        Set-Content (Join-Path $tmp 'restore.csproj') '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="*" /></ItemGroup></Project>'
        dotnet restore (Join-Path $tmp 'restore.csproj') | Out-Null
    }
    $makeappx = Get-ChildItem $nuget -Recurse -Filter makeappx.exe | Where-Object FullName -match '\\x64\\' |
        Sort-Object FullName -Descending | Select-Object -First 1
}
if (-not $makeappx) { throw "makeappx.exe not found" }

& $makeappx.FullName pack /o /d $layout /p $package
if ($LASTEXITCODE) { throw "makeappx failed" }
Write-Host "Package: $package"
