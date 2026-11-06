param([Parameter(Mandatory=$true)][string]$GameDirectory)
$ErrorActionPreference='Stop'
$managed=Join-Path $GameDirectory 'Cursed Words_Data\Managed'
$refs=@('mscorlib','System','System.Core','netstandard','UnityEngine','UnityEngine.CoreModule','UnityEngine.IMGUIModule','UnityEngine.InputLegacyModule','UnityEngine.TextRenderingModule','UnityEngine.TextCoreFontEngineModule','UnityEngine.UIModule','UnityEngine.UI','Unity.TextMeshPro','Assembly-CSharp')
$args=@('/noconfig','/nologo','/target:library','/nostdlib',"/out:$PSScriptRoot\CursedWordsZh.dll")
$args += $refs | ForEach-Object { '/reference:'+(Join-Path $managed ($_.ToString()+'.dll')) }
$args += "$PSScriptRoot\CursedWordsZh.cs"
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' $args

if($LASTEXITCODE -ne 0){throw "Helper compilation failed"}
