param([string]$Version='1.1')
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
if ($Version -notmatch '^\d+(\.\d+){1,3}$') { throw 'Invalid version.' }
$releaseDir=Join-Path $PWD "release/HangeulVR-$Version-Windows-x64"
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
$runtimeFiles=@("HangeulVR-$Version.exe",'openvr_api.dll','actions.json','bindings_oculus_touch.json','bindings_knuckles.json','bindings_vive_controller.json','bindings_holographic_controller.json','OpenVR-LICENSE.txt')
foreach ($file in $runtimeFiles) { Copy-Item -LiteralPath (Join-Path 'dist' $file) -Destination $releaseDir -Force }
Copy-Item -LiteralPath 'release-guide.txt' -Destination (Join-Path $releaseDir '사용안내.txt') -Force
Copy-Item -LiteralPath 'COPYRIGHT.txt' -Destination $releaseDir -Force
Copy-Item -LiteralPath 'vendor/THIRD-PARTY.txt' -Destination $releaseDir -Force
$files=@($runtimeFiles)+@('사용안내.txt','THIRD-PARTY.txt','COPYRIGHT.txt')
$inputs=$files | ForEach-Object { Join-Path $releaseDir $_ }
$zip=Join-Path $PWD "HangeulVR-$Version-Windows-x64.zip"
Compress-Archive -LiteralPath $inputs -DestinationPath $zip -Force
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  HangeulVR-$Version-Windows-x64.zip" | Set-Content -LiteralPath "HangeulVR-$Version-Windows-x64.sha256" -Encoding ASCII
Write-Output "Packaged $zip (no CMD, developer tools, signing key, settings or logs)."
