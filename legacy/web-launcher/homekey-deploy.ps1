# Builds, signs and installs the Home key trampoline (homekey/), then runs it once on the TV,
# which should bring the launcher to the front.
# Signing uses the "TVLauncher" security profile, like launcher-deploy.ps1.
#   .\homekey-deploy.ps1           build + install + run
#   .\homekey-deploy.ps1 -NoRun    build + install only
param([switch]$NoRun)

$tizen = "$env:USERPROFILE\tizen-studio\tools\ide\bin\tizen.bat"
$sdb = "$env:USERPROFILE\tizen-studio\tools\sdb.exe"
$tv = '192.0.2.46:26101'
$src = Join-Path $PSScriptRoot 'homekey'
# Build in a temp copy so signature files never land in the repo.
$build = Join-Path $env:TEMP 'tvhomekey-build'

if (Test-Path $build) { Remove-Item $build -Recurse -Force }
Copy-Item $src $build -Recurse

& $sdb connect $tv | Out-Null

& $tizen package -t wgt -s TVLauncher -- $build
$wgt = Get-ChildItem $build -Filter *.wgt | Select-Object -First 1
if (-not $wgt) { Write-Error 'Packaging failed, no .wgt produced'; exit 1 }
# The TV's installer fails on a filename with a space ("Home key.wgt").
$wgt = Rename-Item $wgt.FullName 'HomeKey.wgt' -PassThru

& $tizen install -n $wgt.Name -s $tv -- $build
if ($LASTEXITCODE -ne 0) { Write-Error 'Install failed'; exit 1 }

if (-not $NoRun) { & $sdb -s $tv shell 0 was_execute HDvHomeKey.Go }
