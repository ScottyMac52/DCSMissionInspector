param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$eventFactoryPath = Join-Path $RepoRoot "DcsMissionReader\Services\PostBriefingWeaponEventResultFactory.cs"

if (-not (Test-Path $eventFactoryPath)) {
    throw "Could not find PostBriefingWeaponEventResultFactory.cs at: $eventFactoryPath"
}

$content = Get-Content -Path $eventFactoryPath -Raw

$content = $content.Replace(
"                SourceName = sourceObject is null ? null : GetDisplayName(sourceObject),
                TargetObjectId = targetObjectId,
                TargetName = targetObject is null ? null : GetDisplayName(targetObject),",
"                SourceName = sourceObject?.Name ?? sourceObject?.Group,
                TargetObjectId = targetObjectId,
                TargetName = targetObject?.Name ?? targetObject?.Group,")

# Remove the local GetDisplayName helper if it exists in this factory.
$content = [regex]::Replace(
    $content,
    "\r?\n        private static string GetDisplayName\(TacviewObjectTrack track\)\r?\n        \{\r?\n            if \(!string\.IsNullOrWhiteSpace\(track\.Group\)\)\r?\n            \{\r?\n                return track\.Group;\r?\n            \}\r?\n\r?\n            if \(!string\.IsNullOrWhiteSpace\(track\.Name\)\)\r?\n            \{\r?\n                return track\.Name;\r?\n            \}\r?\n\r?\n            return track\.ObjectId;\r?\n        \}\r?\n",
    "`r`n")

Set-Content -Path $eventFactoryPath -Value $content -Encoding UTF8

Write-Host "Repaired explicit weapon-result display names."
Write-Host "Now run:"
Write-Host "  dotnet test .\DCSMissionInspector.sln -c Release"
