param(
    [Parameter(Mandatory=$true)]
    [string]$GameDir,

    [switch]$Install
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Src = Join-Path $ProjectRoot "src\OC2BeachPredictorV3Force.cs"
$Dist = Join-Path $ProjectRoot "dist"
$Out = Join-Path $Dist "OC2BeachPredictorV3Force.dll"

if (!(Test-Path $Dist)) {
    New-Item -ItemType Directory -Path $Dist | Out-Null
}

$Managed = Join-Path $GameDir "Overcooked2_Data\Managed"
$BepInExCore = Join-Path $GameDir "BepInEx\core"

if (!(Test-Path $Managed)) {
    throw "Managed directory not found: $Managed"
}
if (!(Test-Path $BepInExCore)) {
    throw "BepInEx core directory not found: $BepInExCore"
}

$cscCandidates = @(
    "$env:WINDIR\Microsoft.NET\Framework\v3.5\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

$csc = $null
foreach ($candidate in $cscCandidates) {
    if (Test-Path $candidate) {
        $csc = $candidate
        break
    }
}

if ($null -eq $csc) {
    throw "Could not find csc.exe. Install .NET Framework build tools."
}

Write-Host "Using compiler: $csc"
Write-Host "GameDir: $GameDir"
Write-Host "Managed: $Managed"

$refs = @()

$requiredRefs = @(
    (Join-Path $BepInExCore "BepInEx.dll"),
    (Join-Path $BepInExCore "0Harmony20.dll"),
    (Join-Path $Managed "Assembly-CSharp.dll"),
    (Join-Path $Managed "UnityEngine.dll")
)

foreach ($r in $requiredRefs) {
    if (Test-Path $r) {
        $refs += $r
    }
}

$unityRefs = Get-ChildItem -Path $Managed -Filter "UnityEngine*.dll" -ErrorAction SilentlyContinue
foreach ($u in $unityRefs) {
    if ($refs -notcontains $u.FullName) {
        $refs += $u.FullName
    }
}

Write-Host "Unity refs found:" $unityRefs.Count
Write-Host "Output:" $Out

$refArgs = $refs | ForEach-Object { "/reference:$_" }

& $csc /target:library /out:$Out /optimize+ /debug- $refArgs $Src

if ($LASTEXITCODE -ne 0) {
    throw "Compile failed."
}

Write-Host "Compile OK:" $Out

if ($Install) {
    $PluginDir = Join-Path $GameDir "BepInEx\plugins"
    if (!(Test-Path $PluginDir)) {
        New-Item -ItemType Directory -Path $PluginDir | Out-Null
    }
    $Dest = Join-Path $PluginDir "OC2BeachPredictorV3Force.dll"
    Copy-Item $Out $Dest -Force
    Write-Host "Installed to:" $Dest
}
