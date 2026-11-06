param([Parameter(Mandatory=$true)][string]$GameDirectory)
$ErrorActionPreference='Stop'
Add-Type -Path "$PSScriptRoot\lib\Mono.Cecil.dll"
Add-Type -Path "$PSScriptRoot\lib\Mono.Cecil.Rocks.dll"
$game=$GameDirectory
$managed=Join-Path $game 'Cursed Words_Data\Managed'
$out=Join-Path $PSScriptRoot '..'
New-Item -ItemType Directory -Force -Path $out,(Join-Path $out 'backup'),(Join-Path $out 'source') | Out-Null
$original=Join-Path $out 'backup\Assembly-CSharp.dll'
if(!(Test-Path $original)){throw 'Missing original assembly backup'}
$resolver=[Mono.Cecil.DefaultAssemblyResolver]::new();$resolver.AddSearchDirectory($managed);$resolver.AddSearchDirectory($PSScriptRoot)
$params=[Mono.Cecil.ReaderParameters]::new();$params.AssemblyResolver=$resolver
$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($original,$params)
$h=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $PSScriptRoot 'CursedWordsZh.dll'),$params)
$ht=$h.MainModule.GetType('CursedWordsZh')
$boot=$a.MainModule.ImportReference(($ht.Methods | Where-Object Name -eq Boot))
$show=$a.MainModule.ImportReference(($ht.Methods | Where-Object Name -eq ShowWords))
$historic=$a.MainModule.ImportReference(($ht.Methods | Where-Object Name -eq ShowHistoric))
foreach($typeName in @('SaveSlotsController','MainMenuOptionsController','EncounterController','PuzzleController')){
 $t=$a.MainModule.GetType($typeName)
 $m=$t.Methods | Where-Object { $_.Name -eq 'Start' -and $_.HasBody } | Select-Object -First 1
 if($m){$il=$m.Body.GetILProcessor();$il.InsertBefore($m.Body.Instructions[0],$il.Create([Mono.Cecil.Cil.OpCodes]::Call,$boot));"Boot hook: $typeName"}
}
# Read the very same HistoricWord.Words used by the character's quip; never resolve a wildcard twice.
$m=$a.MainModule.GetType('EncounterController').Methods | Where-Object Name -eq SubmitWord
[Mono.Cecil.Rocks.MethodBodyRocks]::SimplifyMacros($m.Body)
$il=$m.Body.GetILProcessor()
$constructors=@($m.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Newobj' -and $_.Operand.DeclaringType.FullName -eq 'HistoricWord' })
if($constructors.Count -ne 1){throw 'HistoricWord hook target changed'}
$dup=$il.Create([Mono.Cecil.Cil.OpCodes]::Dup)
$il.InsertAfter($constructors[0],$dup)
$il.InsertAfter($dup,$il.Create([Mono.Cecil.Cil.OpCodes]::Call,$historic))
# The puzzle submission callback has already resolved its canonical word list in local 0.
$m=$a.MainModule.GetType('TileSelectionManager').Methods | Where-Object Name -eq SelectionSubmittedCallback
$il=$m.Body.GetILProcessor()
$targets=@($m.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Callvirt' -and $_.Operand.DeclaringType.FullName -eq 'PuzzleController' -and $_.Operand.Name -eq 'SubmitWord' })
if($targets.Count -ne 1 -or $m.Body.Variables[0].VariableType.FullName -ne 'System.Collections.Generic.List`1<System.String>'){throw 'Puzzle resolved-word hook target changed'}
$il.InsertBefore($targets[0],$il.Create([Mono.Cecil.Cil.OpCodes]::Ldloc_0))
$il.InsertBefore($targets[0],$il.Create([Mono.Cecil.Cil.OpCodes]::Call,$show))
$translate=$a.MainModule.ImportReference(($ht.Methods | Where-Object Name -eq Translate))
$setText=$a.MainModule.ImportReference(($ht.Methods | Where-Object Name -eq SetText))
function PatchTextCalls($type){
 foreach($method in $type.Methods){
  if(!$method.HasBody){continue}
  foreach($instruction in $method.Body.Instructions){
   if($instruction.OpCode.Code -eq 'Callvirt' -and $instruction.Operand -is [Mono.Cecil.MethodReference] -and ($instruction.Operand.Name -eq 'set_text' -or $instruction.Operand.Name -eq 'SetText') -and $instruction.Operand.Parameters.Count -eq 1 -and $instruction.Operand.Parameters[0].ParameterType.FullName -eq 'System.String' -and $instruction.Operand.DeclaringType.FullName -eq 'TMPro.TMP_Text'){
    $instruction.OpCode=[Mono.Cecil.Cil.OpCodes]::Call;$instruction.Operand=$setText
   }
  }
 }
 foreach($nested in $type.NestedTypes){PatchTextCalls $nested}
}
foreach($type in $a.MainModule.Types){PatchTextCalls $type}
$m=$a.MainModule.GetType('RewardObject').Methods | Where-Object Name -eq Populate
$il=$m.Body.GetILProcessor()
foreach($instruction in @($m.Body.Instructions)) {
 if($instruction.OpCode.Code -eq 'Ldfld' -and $instruction.Operand.Name -eq 'RewardDescription') {
  $il.InsertAfter($instruction,$il.Create([Mono.Cecil.Cil.OpCodes]::Call,$translate))
 }
}

$reviewedKeys=[System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$reviewed=Get-Content (Join-Path $PSScriptRoot 'reviewed.json') -Raw | ConvertFrom-Json -AsHashtable
foreach($key in $reviewed.Keys){[void]$reviewedKeys.Add($key)}
foreach($name in @('DialogueUtility','ShopRecStrings')){
 foreach($method in $a.MainModule.GetType($name).Methods){
  if($method.Name -ne '.cctor' -or !$method.HasBody){continue}
  [Mono.Cecil.Rocks.MethodBodyRocks]::SimplifyMacros($method.Body)
  $il=$method.Body.GetILProcessor()
  foreach($instruction in @($method.Body.Instructions)){
   if($instruction.OpCode.Code -eq 'Ldstr' -and $instruction.Operand.Length -gt 5 -and $reviewedKeys.Contains($instruction.Operand)){
    $il.InsertAfter($instruction,$il.Create([Mono.Cecil.Cil.OpCodes]::Call,$translate))
   }
  }
 }
}
foreach($spec in @(@('DialogueController','WriteOutText'),@('DialogueController','WobbleText'),@('TutorialController','NewSpeechBubble'))){
 $m=$a.MainModule.GetType($spec[0]).Methods | Where-Object Name -eq $spec[1]
 $il=$m.Body.GetILProcessor();$first=$m.Body.Instructions[0]
 $il.InsertBefore($first,$il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_1))
 $il.InsertBefore($first,$il.Create([Mono.Cecil.Cil.OpCodes]::Call,$translate))
 $il.InsertBefore($first,$il.Create([Mono.Cecil.Cil.OpCodes]::Starg,$m.Parameters[0]))
}
foreach($name in @('UseAn','RandomiseWobble')){
 $m=$a.MainModule.GetType('DialogueUtility').Methods | Where-Object Name -eq $name
 $il=$m.Body.GetILProcessor();$first=$m.Body.Instructions[0]
 $il.InsertBefore($first,$il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
 $il.InsertBefore($first,$il.Create([Mono.Cecil.Cil.OpCodes]::Call,$translate))
 $il.InsertBefore($first,$il.Create([Mono.Cecil.Cil.OpCodes]::Starg,$m.Parameters[0]))
}
$a.Write((Join-Path $out 'payload\Assembly-CSharp.dll'));$a.Dispose();$h.Dispose()
Copy-Item (Join-Path $PSScriptRoot 'CursedWordsZh.dll') (Join-Path $out 'payload\CursedWordsZh.dll') -Force
Write-Host 'Payload generated; installed game was not changed.'
