param([switch]$IncludeDevTools)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '.NET Framework 4.x C# compiler not found.' }
$expectedOpenVrHash = 'BAB8AC6EF64E68A9CA53315B0014D131088584B2EFDFA6DB511D67EC03CFCB4A'
if ((Get-FileHash -LiteralPath 'dist/openvr_api.dll' -Algorithm SHA256).Hash -ne $expectedOpenVrHash) { throw 'OpenVR DLL differs from the pinned distribution.' }
node native/generate-bindings.mjs
if ($LASTEXITCODE -ne 0) { throw 'Binding generation failed.' }
$compilerArgs = @('/nologo','/platform:x64','/optimize+','/win32icon:assets\hangeul.ico','/resource:assets\hangeul.ico,HangeulVR.AppIcon','/resource:native\update-public-key.xml,HangeulVR.UpdatePublicKey','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:System.Runtime.Serialization.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll')
$sources = @('vendor\openvr_api.cs','native\Core.cs','native\Editing.cs','native\Preferences.cs','native\Keyboard.cs','native\D3DTexture.cs','native\SmoothRendering.cs','native\Overlay.cs','native\NativeLibrarySecurity.cs','native\Program.cs','native\AppServices.cs','native\SteamVrStartup.cs','native\Updates.cs','native\LauncherPanel.cs')
& $compilerPath @compilerArgs /target:winexe /out:dist\HangeulVR-1.2.exe @sources
if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
Write-Output 'Built dist/HangeulVR-1.2.exe (release; no developer test commands).'
if ($IncludeDevTools) {
    & $compilerPath @compilerArgs /target:exe /define:DEVELOPMENT /out:dist\HangeulVR-DevTools.exe @sources native\SelfTest.cs native\UpdateTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Developer tools build failed.' }
    Write-Output 'Built dist/HangeulVR-DevTools.exe (local testing only; do not distribute).'
}
