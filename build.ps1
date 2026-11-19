param([Parameter(Mandatory=$true)][string]$LiveSplitRoot)
$ErrorActionPreference = 'Stop'
$LiveSplitRoot = (Resolve-Path -LiteralPath $LiveSplitRoot).Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$references = @('LiveSplit.Core.dll','UpdateManager.dll','SharpDX.dll','SharpDX.DirectInput.dll') | ForEach-Object { Join-Path $LiveSplitRoot $_ }
$references += @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Xml.dll')
$arguments = @('/nologo','/target:library','/optimize+',('/out:' + (Join-Path $dist 'LiveSplit.CupheadInputDisplay.dll')))
$arguments += $references | ForEach-Object { '/r:' + $_ }
$arguments += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
$assets = Join-Path $PSScriptRoot 'assets'
$arguments += Get-ChildItem -LiteralPath $assets -File -Recurse | ForEach-Object {
    '/resource:' + $_.FullName + ',CupheadInputDisplay.Asset/assets/' + $_.FullName.Substring($assets.Length + 1).Replace('\','/')
}
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host 'Built dist\LiveSplit.CupheadInputDisplay.dll'
