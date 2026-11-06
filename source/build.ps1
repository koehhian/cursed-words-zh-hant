param([Parameter(Mandatory=$true)][string]$GameDirectory)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'compile.ps1') -GameDirectory $GameDirectory
& (Join-Path $PSScriptRoot 'patch.ps1') -GameDirectory $GameDirectory
$ErrorActionPreference='Stop'
python (Join-Path $PSScriptRoot 'build_resources.py')
if($LASTEXITCODE -ne 0){throw '打包資料失敗'}
New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot '..\distribution') | Out-Null
$args=@('/noconfig','/nologo','/target:winexe','/optimize+','/codepage:65001',('/out:'+(Join-Path $PSScriptRoot '..\distribution\CursedWordsZHant.exe')),'/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll')
foreach($name in @('assembly','helper','translations','dictionary','license')){
 $args+='/resource:'+(Join-Path $PSScriptRoot ('installer-resources\'+$name+'.gz'))+',CWZh.'+$name
}
$args+=Join-Path $PSScriptRoot 'CursedWordsInstaller.cs'
$args+=Join-Path $PSScriptRoot 'installer-resources\GeneratedBuild.cs'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' $args
if($LASTEXITCODE -ne 0){throw 'EXE 編譯失敗'}
