# DCS Mission Inspector

**DCS Mission Inspector** is a command-line utility for inspecting Digital Combat Simulator World mission files (`.miz`) and generating readable mission reports, data exports, and Google Earth mapping output.

The main executable is `DcsMissionReader.exe`. It can process one or more `.miz` files, extract mission metadata and embedded resources, and create output folders containing HTML, JSON, and KML artifacts. It can also create post-briefing Google Earth output from zipped Tacview ACMI files (`.zip.acmi`).

---

## What It Does

DCS Mission Inspector reads DCS mission archives and helps answer questions like:

- What aircraft, helicopters, ships, ground units, and static objects are in the mission?
- What player/client slots are available?
- What are the routes, waypoints, speeds, altitudes, and tasking for each flight?
- What briefing text, briefing images, and kneeboards are packaged with the mission?
- What threats, weapons, and target areas are present?
- What does the mission look like in Google Earth?
- What happened after the mission when using a Tacview `.zip.acmi` file?

---

## Current Capabilities

- **HTML mission report**
  - Mission metadata, briefing, blue/red tasking, images, kneeboards, player slots, flights, waypoints, ATO, units/targets, weather, and order of battle.
- **JSON summary export**
  - High-level mission information suitable for review or downstream tooling.
- **Full JSON export**
  - Raw mission table, dictionary data, and extracted archive file list.
- **KML pre-brief export**
  - Google Earth routes, waypoints, ground units, ships, static objects, and threat visualization.
- **Tacview post-brief KMZ export**
  - Creates a post-briefing Google Earth file from a zipped Tacview ACMI file.
- **Windows shell registration support**
  - Registry integration for adding DCS Mission Inspector actions to `.miz` and Tacview ACMI shell menus.
- **Multiple mission processing**
  - Process more than one `.miz` file in a single command.
- **Unit selection**
  - Default real-world/imperial output, with optional metric output.

---

## Repository Layout

```text
DCSMissionInspector/
├─ DcsMissionReader/          Main command-line application
├─ DcsMissionReaderTests/     Test project
├─ LuaParser/                 Lua parsing/test utility project
├─ dcsmissionreader.reg       Example Windows shell registration file
├─ DcsMissionInspector.sln    Visual Studio solution
└─ README.md
```

---

## Requirements

### To Run a Published Build

- Windows
- DCS World `.miz` files
- Optional: Google Earth or another KML/KMZ viewer
- Optional: Tacview zipped ACMI files for post-brief output

### To Build from Source

- Visual Studio 2022 or later
- .NET 10 SDK

The main project currently targets `net10.0`.

---

## Build from Source

From the repository root:

```powershell
dotnet restore .\DcsMissionInspector.sln
dotnet build .\DcsMissionInspector.sln -c Release
dotnet test .\DcsMissionInspector.sln -c Release
```

To publish a Windows x64 single-file executable:

```powershell
dotnet publish .\DcsMissionReader\DcsMissionReader.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true
```

The published executable will be under:

```text
DcsMissionReader\bin\Release\net10.0\win-x64\publish\
```

---

## Command-Line Usage

```text
DcsMissionReader.exe [options] <mission1.miz> [mission2.miz ...]
DcsMissionReader.exe --post-brief <sortie.zip.acmi>
```

### Options

| Option | Aliases | Description |
|---|---|---|
| `-h` | `-?`, `--help` | Show help. |
| `-v` | `--ver`, `--version` | Show application version. |
| `--html` | `--create-html`, `--out-html` | Generate the HTML mission report. |
| `-j` | `--json`, `--out-json` | Generate `mission_summary.json`. |
| `-f` | `--full`, `--full-export` | Generate `mission_full.json`. |
| `-k` | `--kml`, `--google-earth` | Generate Google Earth KML output. |
| `--metric` | | Use metric units. Default is real-world/imperial. |
| `-c` | `--check`, `--check-registration` | Check shell registration. |
| `-i` | `--install`, `--install-registration` | Install shell registration. |
| `-u` | `--uninstall`, `--uninstall-registration` | Uninstall shell registration. |
| `--post-brief` | `--post_brief`, `--postbrief` | Generate post-brief KMZ from a zipped Tacview ACMI file. |

> Important: `-h` means **help**, not HTML. Use `--html` to generate the HTML report.

---

## Examples

### Generate the HTML report for one mission

```powershell
DcsMissionReader.exe --html "C:\DCS\Missions\My Mission.miz"
```

### Generate HTML, JSON summary, and KML

```powershell
DcsMissionReader.exe --html --json --kml "C:\DCS\Missions\My Mission.miz"
```

### Generate every pre-brief export

```powershell
DcsMissionReader.exe --html --json --full --kml "C:\DCS\Missions\My Mission.miz"
```

### Process multiple missions

```powershell
DcsMissionReader.exe --html --json --metric `
  "C:\DCS\Missions\Mission 1.miz" `
  "C:\DCS\Missions\Mission 2.miz"
```

### Generate only Google Earth KML

```powershell
DcsMissionReader.exe --kml "C:\DCS\Missions\My Mission.miz"
```

### Generate a Tacview post-brief KMZ

```powershell
DcsMissionReader.exe --post-brief "C:\Tacview\Sortie.zip.acmi"
```

---

## Output Structure

For each `.miz` file, the tool creates a report folder in the current working directory:

```text
<Mission Name>_Report/
├─ index.html             HTML report, if --html was used
├─ mission_summary.json   JSON summary, if --json was used
├─ mission_full.json      Full raw export, if --full was used
├─ <Mission Name>.kml     Google Earth KML, if --kml was used
├─ images/                Briefing images copied from the mission archive
└─ kneeboards/            Kneeboard files copied from the mission archive
```

For post-brief Tacview processing, the tool creates:

```text
<Sortie Name>_PostBrief_Report/
└─ <Sortie Name>.postbrief.kmz
```

---

## HTML Report Contents

The HTML report is usually the most useful output. It includes:

- Mission name, map, date, start time, and mission version
- Briefing text
- Blue and red tasking text
- Briefing images
- Custom kneeboards
- Required mods
- Player/client slots
- Flights and waypoints
- Embedded route maps
- Air Tasking Order summary
- Units and targets
- Weather
- Order of Battle totals and coalition breakdowns

---

## KML / KMZ Output

The pre-brief KML export is intended for route and target planning in Google Earth. It includes:

- Coalition-colored aircraft and helicopter routes
- Waypoint placemarks
- Ground unit, ship, and static object placemarks
- Threat range visualization where threat data is available

The post-brief KMZ export is intended for after-action review using Tacview `.zip.acmi` data. It includes:

- Group tracks
- Weapon employments
- Weapon results
- Reduced track-point density to keep the generated KMZ manageable

---

## Windows Shell Registration

The repository includes `dcsmissionreader.reg` as an example registry file for adding Explorer context-menu actions.

The application also supports registration commands:

```powershell
DcsMissionReader.exe --check-registration
DcsMissionReader.exe --install-registration
DcsMissionReader.exe --uninstall-registration
```

Depending on where the executable is installed, the registry command paths may need to be adjusted before importing or installing registration entries.

---

## Development Notes

- Main application: `DcsMissionReader`
- Test project: `DcsMissionReaderTests`
- Lua parsing utility: `LuaParser`
- Dependency injection is configured in `Program.cs`
- Export behavior is strategy-based through `IMissionExportStrategy`
- Current export strategies include:
  - `HtmlReportGenerator`
  - `JsonSummaryGenerator`
  - `KmlExportGenerator`
  - `PostBriefingExportGenerator`
- Threat and weapon lookup data are loaded from JSON files under `DcsMissionReader/Data`

---

## License

MIT License. See [LICENSE](LICENSE).
