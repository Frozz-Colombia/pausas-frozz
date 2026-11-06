# Compila PausasFrozz.exe con el compilador C# incluido en Windows (.NET Framework 4).
# Uso:  powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$src = Join-Path $PSScriptRoot 'src'
$out = Join-Path $PSScriptRoot 'PausasFrozz.exe'
$ico = Join-Path $src 'app.ico'

function Compile([string]$iconArg) {
    $a = @('/nologo', '/target:winexe', '/optimize+', "/out:$out",
           "/resource:$src\instructivo.jpg,instructivo.jpg",
           '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll')
    if ($iconArg) { $a += "/win32icon:$iconArg" }
    $a += "$src\Program.cs"
    & $csc @a
    if ($LASTEXITCODE -ne 0) { throw "Error de compilación" }
}

if (-not (Test-Path $ico)) {
    Compile $null
    Start-Process -FilePath $out -ArgumentList '--export-icon', "`"$ico`"" -Wait
}
Compile $ico
Write-Host "OK -> $out"
