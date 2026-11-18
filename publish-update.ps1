param(
    [Parameter(Mandatory=$true)][string]$Repository,
    [string]$Version='1.1',
    [string]$Notes='자동 실행 및 앱 내 업데이트 기능',
    [string]$Tag,
    [string]$OutputDirectory
)
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
if ($Repository -notmatch '^[A-Za-z0-9][A-Za-z0-9-]{0,38}/[A-Za-z0-9_.-]{1,100}$') { throw 'Repository must be owner/repository.' }
if ($Version -notmatch '^\d+(\.\d+){1,3}$') { throw 'Version must be numeric, such as 1.1.' }
if (-not $Tag) { $Tag="v$Version" }
if ($Tag -notmatch '^[A-Za-z0-9_.-]+$') { throw 'Tag must use letters, digits, dots, underscores or hyphens.' }
$zipName="HangeulVR-$Version-Windows-x64.zip"
$zipPath=Join-Path $PWD $zipName
if (-not (Test-Path -LiteralPath $zipPath)) { throw "Missing $zipName. Run package-release.ps1 first." }
$outDir=if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $PWD "update-publish/$Version" }
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$manifest=[ordered]@{
 Version=$Version
 PackageUrl="https://github.com/$Repository/releases/download/$Tag/$zipName"
 Sha256=(Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
 PackageBytes=(Get-Item -LiteralPath $zipPath).Length
 EntryPoint="HangeulVR-$Version.exe"
 Notes=$Notes
}
$json=$manifest | ConvertTo-Json
$bytes=[Text.Encoding]::UTF8.GetBytes($json)
Add-Type -AssemblyName System.Security.Cryptography.ProtectedData
$protectedKey=[IO.File]::ReadAllBytes((Join-Path $PWD '.local-signing/update-key.dpapi'))
$privateBytes=[System.Security.Cryptography.ProtectedData]::Unprotect($protectedKey,$null,[System.Security.Cryptography.DataProtectionScope]::CurrentUser)
$rsa=[System.Security.Cryptography.RSA]::Create()
try {
 $rsa.FromXmlString([Text.Encoding]::UTF8.GetString($privateBytes))
 if ($rsa.ToXmlString($false) -ne [IO.File]::ReadAllText((Join-Path $PWD 'native/update-public-key.xml'))) { throw 'Private key does not match the application update key.' }
 $signature=$rsa.SignData($bytes,[System.Security.Cryptography.HashAlgorithmName]::SHA256,[System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
 [IO.File]::WriteAllBytes((Join-Path $outDir 'update.json'),$bytes)
 [IO.File]::WriteAllBytes((Join-Path $outDir 'update.json.sig'),$signature)
} finally { $rsa.Dispose();[Array]::Clear($privateBytes,0,$privateBytes.Length) }
Copy-Item -LiteralPath $zipPath -Destination $outDir -Force
Write-Output "Prepared signed update assets in $outDir. Upload its three files to the public GitHub Release tagged $Tag and mark it Latest. Nothing was uploaded by this script."
