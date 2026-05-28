#Requires -Version 5.1
<#
.SYNOPSIS
    Dev convenience: patch Painscreek's Assembly-CSharp.dll for fast boot to gameplay.

.DESCRIPTION
    Speeds up the StartMenu flow and level transitions so we can iterate on
    head-tracking work without sitting through fades, the disclaimer animation,
    the prologue narration, or level fade-outs every launch. Not part of the
    shipped mod.

    Patches applied to Assembly-CSharp.dll via Mono.Cecil:

      StartMenu.Start (tail):
        - Zero screenfadein_delay, loadlevel_delay, disclaimer_delay,
          Prologue_delay, canSkip_time.
        - Auto-invoke Continue() if a save exists, otherwise
          DeleteAndStartNewGame().

      StartMenu.NewGameStart iterator MoveNext:
        - Replace StartCoroutine(PlayDisclaimer()) with StartCoroutine(Skip()),
          which jumps straight to LoadVillage and skips the disclaimer +
          prologue voiceover entirely.
        - Zero every WaitForSeconds in the iterator (kills the menu fade-out
          wait before the scene async-load completes).

      StartMenu.FadeIn iterator MoveNext:
        - Zero every WaitForSeconds (kills the menu intro fade-in wait that
          gates the auto-Continue / auto-NewGame click).

      PlayerControl.FadeOut:
        - Append `this.fadeTime = 0f` so LoadLevel.LoadingLevel and
          StartMenu.ContinueGame don't wait on the fade animation before
          unloading the current scene.

    The first run backs up the original to Assembly-CSharp.dll.fastboot-backup.
    Subsequent runs restore that backup first, then re-apply, so iterating on
    the patch set is a no-fuss `pixi run dev-fastboot`.

    To revert, run scripts/dev-fastboot-restore.ps1 or pixi run dev-fastboot-restore.

.PARAMETER GamePath
    Optional explicit path to the game root. Auto-detected if omitted.
#>
param(
    [Parameter(Mandatory=$false)]
    [string]$GamePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
. (Join-Path $scriptDir 'common.ps1')

if (-not $GamePath) {
    $GamePath = Resolve-GamePath
    if (-not $GamePath) {
        Show-GameNotFoundError
        exit 1
    }
}

$managed = Get-ManagedPath -GamePath $GamePath
$asmPath = Join-Path $managed 'Assembly-CSharp.dll'
if (-not (Test-Path $asmPath)) {
    Write-Error "Assembly-CSharp.dll not found at: $asmPath"
    exit 1
}

$backupPath = "$asmPath.fastboot-backup"
if (Test-Path $backupPath) {
    Copy-Item -Path $backupPath -Destination $asmPath -Force
    Write-Host "  Restored from backup before re-patching -> $backupPath" -ForegroundColor DarkGray
} else {
    Copy-Item -Path $asmPath -Destination $backupPath -Force
    Write-Host "  Backed up original -> $backupPath" -ForegroundColor Green
}

$toolsDir = Join-Path $projectRoot 'tools'
$cecilPath = & (Join-Path $scriptDir 'ensure-cecil.ps1') -ToolsDir $toolsDir
Add-Type -Path $cecilPath

$marker = 'HeadTracking_FastBoot_v2'

$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory($managed)
$readerParams = [Mono.Cecil.ReaderParameters]::new()
$readerParams.AssemblyResolver = $resolver
$readerParams.ReadWrite = $false
$readerParams.InMemory = $true

$bytes = [System.IO.File]::ReadAllBytes($asmPath)
$ms = [System.IO.MemoryStream]::new($bytes)
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($ms, $readerParams)
try {
    $module = $asm.MainModule
    $ts = $module.TypeSystem
    $OpCodes = [Mono.Cecil.Cil.OpCodes]

    if ($module.Types | Where-Object { $_.Name -eq $marker }) {
        Write-Host "  Already fast-booted (marker present) - nothing to do." -ForegroundColor Yellow
        return
    }

    $startMenu = $module.GetType('StartMenu')
    if (-not $startMenu) { throw "Type 'StartMenu' not found in Assembly-CSharp.dll" }

    function Get-Field($type, $name) {
        $f = $type.Fields | Where-Object { $_.Name -eq $name } | Select-Object -First 1
        if (-not $f) { throw "Field '$name' not found on $($type.FullName)" }
        return $f
    }
    function Get-Method($type, $name) {
        $m = $type.Methods | Where-Object { $_.Name -eq $name } | Select-Object -First 1
        if (-not $m) { throw "Method '$name' not found on $($type.FullName)" }
        return $m
    }

    $fld_screenfadein = Get-Field $startMenu 'screenfadein_delay'
    $fld_loadlevel    = Get-Field $startMenu 'loadlevel_delay'
    $fld_disclaimer   = Get-Field $startMenu 'disclaimer_delay'
    $fld_prologue     = Get-Field $startMenu 'Prologue_delay'
    $fld_canSkipTime  = Get-Field $startMenu 'canSkip_time'
    $fld_continueBtn  = Get-Field $startMenu 'continueButton'

    $startMethod              = Get-Method $startMenu 'Start'
    $continueMethod           = Get-Method $startMenu 'Continue'
    $deleteAndNewGameMethod   = Get-Method $startMenu 'DeleteAndStartNewGame'

    # ---------- Patch 1: end of Start() ----------
    $il = $startMethod.Body.GetILProcessor()
    $body = $startMethod.Body

    $unityGameObject = $null
    foreach ($r in $module.AssemblyReferences) {
        $res = $resolver.Resolve($r)
        if ($res -and $res.MainModule.GetType('UnityEngine.GameObject')) {
            $unityGameObject = $res.MainModule.GetType('UnityEngine.GameObject')
            break
        }
    }
    if (-not $unityGameObject) { throw "Could not resolve UnityEngine.GameObject" }
    $activeSelfProp = $unityGameObject.Properties | Where-Object { $_.Name -eq 'activeSelf' } | Select-Object -First 1
    $activeSelfGetter = $module.ImportReference($activeSelfProp.GetMethod)

    $continueRef         = $module.ImportReference($continueMethod)
    $deleteAndNewGameRef = $module.ImportReference($deleteAndNewGameMethod)

    $retInstr = $body.Instructions[$body.Instructions.Count - 1]
    if ($retInstr.OpCode -ne $OpCodes::Ret) {
        throw "Expected Start() to end with Ret, got $($retInstr.OpCode)"
    }

    function Insert-Before($processor, $anchor, $instrs) {
        foreach ($i in $instrs) { $processor.InsertBefore($anchor, $i) }
    }

    $zeroDelaysInstrs = @(
        $il.Create($OpCodes::Ldarg_0), $il.Create($OpCodes::Ldc_R4, [single]0), $il.Create($OpCodes::Stfld, $fld_screenfadein),
        $il.Create($OpCodes::Ldarg_0), $il.Create($OpCodes::Ldc_R4, [single]0), $il.Create($OpCodes::Stfld, $fld_loadlevel),
        $il.Create($OpCodes::Ldarg_0), $il.Create($OpCodes::Ldc_R4, [single]0), $il.Create($OpCodes::Stfld, $fld_disclaimer),
        $il.Create($OpCodes::Ldarg_0), $il.Create($OpCodes::Ldc_R4, [single]0), $il.Create($OpCodes::Stfld, $fld_prologue),
        $il.Create($OpCodes::Ldarg_0), $il.Create($OpCodes::Ldc_R4, [single]0), $il.Create($OpCodes::Stfld, $fld_canSkipTime)
    )
    Insert-Before $il $retInstr $zeroDelaysInstrs

    # if (continueButton.activeSelf) Continue(); else DeleteAndStartNewGame();
    $elseBranch = $il.Create($OpCodes::Ldarg_0)
    $afterBlock = $retInstr

    Insert-Before $il $retInstr @(
        $il.Create($OpCodes::Ldarg_0),
        $il.Create($OpCodes::Ldfld, $fld_continueBtn),
        $il.Create($OpCodes::Callvirt, $activeSelfGetter),
        $il.Create($OpCodes::Brfalse, $elseBranch),
        $il.Create($OpCodes::Ldarg_0),
        $il.Create($OpCodes::Call, $continueRef),
        $il.Create($OpCodes::Br, $afterBlock),
        $elseBranch,
        $il.Create($OpCodes::Call, $deleteAndNewGameRef)
    )

    # ---------- Patch 2: NewGameStart iterator -> swap PlayDisclaimer for Skip, zero WaitForSeconds ----------
    $newGameIter = $startMenu.NestedTypes | Where-Object { $_.Name -like '<NewGameStart>*' } | Select-Object -First 1
    if (-not $newGameIter) { throw "Could not locate NewGameStart iterator nested type" }
    $newGameMoveNext = Get-Method $newGameIter 'MoveNext'

    $playDisclaimerMethod = Get-Method $startMenu 'PlayDisclaimer'
    $skipMethod           = Get-Method $startMenu 'Skip'
    $skipRef              = $module.ImportReference($skipMethod)

    $replaced = 0
    foreach ($instr in @($newGameMoveNext.Body.Instructions)) {
        if (($instr.OpCode -eq $OpCodes::Call -or $instr.OpCode -eq $OpCodes::Callvirt) `
                -and $instr.Operand -is [Mono.Cecil.MethodReference] `
                -and $instr.Operand.Name -eq 'PlayDisclaimer') {
            $newInstr = $newGameMoveNext.Body.GetILProcessor().Create($instr.OpCode, $skipRef)
            $newGameMoveNext.Body.GetILProcessor().Replace($instr, $newInstr)
            $replaced++
        }
    }
    if ($replaced -eq 0) {
        throw "Did not find PlayDisclaimer call in NewGameStart iterator to replace"
    }

    # ---------- Patch 3: zero every WaitForSeconds(float) in NewGameStart + FadeIn iterators ----------
    # Strategy: find each `newobj WaitForSeconds(.ctor(float))`; insert `pop; ldc.r4 0`
    # immediately before it. The original float-loading IL still runs, we pop its
    # result, push 0, and let newobj consume the 0. Position-stable, doesn't need
    # us to walk the stack backwards to find the original float source.
    function Zero-WaitForSeconds($method) {
        $proc = $method.Body.GetILProcessor()
        $count = 0
        foreach ($instr in @($method.Body.Instructions)) {
            if ($instr.OpCode -ne $OpCodes::Newobj) { continue }
            if (-not ($instr.Operand -is [Mono.Cecil.MethodReference])) { continue }
            $ctor = $instr.Operand
            if ($ctor.DeclaringType.FullName -ne 'UnityEngine.WaitForSeconds') { continue }
            if ($ctor.Parameters.Count -ne 1) { continue }
            $proc.InsertBefore($instr, $proc.Create($OpCodes::Pop))
            $proc.InsertBefore($instr, $proc.Create($OpCodes::Ldc_R4, [single]0))
            $count++
        }
        return $count
    }

    $zeroedNewGame = Zero-WaitForSeconds $newGameMoveNext
    Write-Host "  NewGameStart: zeroed $zeroedNewGame WaitForSeconds call(s)" -ForegroundColor DarkGray

    $fadeInIter = $startMenu.NestedTypes | Where-Object { $_.Name -like '<FadeIn>*' } | Select-Object -First 1
    if (-not $fadeInIter) { throw "Could not locate FadeIn iterator nested type" }
    $fadeInMoveNext = Get-Method $fadeInIter 'MoveNext'
    $zeroedFadeIn = Zero-WaitForSeconds $fadeInMoveNext
    Write-Host "  FadeIn: zeroed $zeroedFadeIn WaitForSeconds call(s)" -ForegroundColor DarkGray

    # ---------- Patch 4: PlayerControl.FadeOut -> append this.fadeTime = 0f ----------
    $playerControl = $module.GetType('PlayerControl')
    if (-not $playerControl) { throw "Type 'PlayerControl' not found in Assembly-CSharp.dll" }
    $fld_fadeTime = Get-Field $playerControl 'fadeTime'
    $fadeOutMethod = $playerControl.Methods | Where-Object {
        $_.Name -eq 'FadeOut' -and $_.Parameters.Count -eq 0 -and $_.ReturnType.FullName -eq 'System.Void'
    } | Select-Object -First 1
    if (-not $fadeOutMethod) { throw "Could not find PlayerControl.FadeOut() : void" }

    $foProc = $fadeOutMethod.Body.GetILProcessor()
    $foBody = $fadeOutMethod.Body
    $foRet = $foBody.Instructions[$foBody.Instructions.Count - 1]
    if ($foRet.OpCode -ne $OpCodes::Ret) {
        throw "Expected PlayerControl.FadeOut() to end with Ret, got $($foRet.OpCode)"
    }
    Insert-Before $foProc $foRet @(
        $foProc.Create($OpCodes::Ldarg_0),
        $foProc.Create($OpCodes::Ldc_R4, [single]0),
        $foProc.Create($OpCodes::Stfld, $fld_fadeTime)
    )

    # ---------- Marker ----------
    $markerType = [Mono.Cecil.TypeDefinition]::new(
        'PainscreekHeadTracking.FastBoot',
        $marker,
        [Mono.Cecil.TypeAttributes]::NotPublic -bor [Mono.Cecil.TypeAttributes]::Class,
        $ts.Object)
    $module.Types.Add($markerType)

    $asm.Write($asmPath)
    Write-Host "  Fast-boot v2 patches applied:" -ForegroundColor Green
    Write-Host "    - Start() auto-launches Continue() or DeleteAndStartNewGame()" -ForegroundColor DarkGreen
    Write-Host "    - StartMenu delays zeroed (fadein/loadlevel/disclaimer/prologue/canSkip)" -ForegroundColor DarkGreen
    Write-Host "    - NewGameStart: PlayDisclaimer -> Skip + WaitForSeconds zeroed" -ForegroundColor DarkGreen
    Write-Host "    - FadeIn: WaitForSeconds zeroed (menu intro fade)" -ForegroundColor DarkGreen
    Write-Host "    - PlayerControl.FadeOut: fadeTime = 0 (level-to-level + Continue)" -ForegroundColor DarkGreen
} finally {
    $asm.Dispose()
    $ms.Dispose()
}
