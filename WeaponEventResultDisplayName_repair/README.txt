Repairs the final post-briefing factory extraction test failure.

Problem:
PostBriefingWeaponEventResultFactory changed explicit weapon-result TargetName/SourceName to group-first.
The original behavior was name-first:
    sourceObject?.Name ?? sourceObject?.Group
    targetObject?.Name ?? targetObject?.Group

That is why the valid zipped ACMI test lost:
    Destroyed - Target Truck

Run from repository root:
    .\RepairWeaponEventResultDisplayNames.ps1
    dotnet test .\DCSMissionInspector.sln -c Release

Expected:
    184 passed
