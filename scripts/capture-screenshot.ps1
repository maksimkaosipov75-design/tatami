# Captures the screen into docs\images\ for the README.
#
# Usage:
#   pwsh -File capture-screenshot.ps1 -Name desktop
#   pwsh -File capture-screenshot.ps1 -Name launchpad -DelaySeconds 5
#
# -DelaySeconds gives you time to open whatever should be on screen (the
# Launchpad, a menu) before the shot is taken.

param(
    [Parameter(Mandatory = $true)][string]$Name,
    [int]$DelaySeconds = 0
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

if ($DelaySeconds -gt 0) {
    Write-Host "Capturing '$Name' in $DelaySeconds seconds - set up the screen now..."
    for ($i = $DelaySeconds; $i -gt 0; $i--) {
        Write-Host "  $i..."
        Start-Sleep -Seconds 1
    }
}

$bounds = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)

$outputDir = Join-Path $PSScriptRoot '..\docs\images'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$path = Join-Path $outputDir "$Name.png"

$bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()

Write-Host "Saved $path ($($bounds.Width)x$($bounds.Height))"
