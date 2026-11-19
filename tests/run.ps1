param([Parameter(Mandatory=$true)][string]$LiveSplitRoot)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$LiveSplitRoot = (Resolve-Path -LiteralPath $LiveSplitRoot).Path
& (Join-Path $root 'build.ps1') -LiveSplitRoot $LiveSplitRoot
$hostDirectory = Join-Path $root 'artifacts\testhost'
$reports = Join-Path $root 'artifacts\reports'
New-Item -ItemType Directory -Force $hostDirectory,$reports,(Join-Path $hostDirectory 'Components') | Out-Null
Get-ChildItem -LiteralPath $LiveSplitRoot -Filter '*.dll' | ForEach-Object {
    [IO.File]::WriteAllBytes((Join-Path $hostDirectory $_.Name),[IO.File]::ReadAllBytes($_.FullName))
}
Copy-Item -LiteralPath (Join-Path $root 'dist\LiveSplit.CupheadInputDisplay.dll') -Destination $hostDirectory -Force
Copy-Item -LiteralPath (Join-Path $LiveSplitRoot 'Components\LiveSplit.Cuphead.dll') -Destination (Join-Path $hostDirectory 'Components') -Force
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
Push-Location $hostDirectory
try {
    foreach ($testName in @('NativeTest','StyleTest')) {
        $arguments = @('/nologo',('/out:' + $testName + '.exe'),'/r:LiveSplit.CupheadInputDisplay.dll','/r:LiveSplit.Core.dll','/r:UpdateManager.dll','/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll','/r:System.Xml.dll',(Join-Path $PSScriptRoot ($testName + '.cs')))
        & $compiler @arguments
        if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $testName" }
        '<configuration><runtime><loadFromRemoteSources enabled="true"/></runtime></configuration>' | Set-Content -LiteralPath ($testName + '.exe.config')
        & (Join-Path $hostDirectory ($testName + '.exe')) $reports | Tee-Object -FilePath (Join-Path $reports ($testName + '.txt'))
        if ($LASTEXITCODE -ne 0) { throw "Test failed: $testName" }
    }
} finally { Pop-Location }
