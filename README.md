# DCS Mission Inspector

**Inspects DCS World mission files (.miz) and generates rich reports, data exports, and visualizations.**

This tool reads Digital Combat Simulator (DCS) World mission files (`.miz` files) and extracts everything you need to understand your mission: briefings, kneeboards, player slots, flight plans, waypoints, Air Tasking Orders, Order of Battle, weather, units, and more. It outputs beautiful HTML reports, JSON data, and KML files for Google Earth.

---

## Features

- **HTML Report** – A complete, self-contained web page with everything about your mission (expandable sections, embedded route maps, image galleries, and kneeboard viewer).
- **JSON Exports** – Summary data or full raw mission data.
- **KML Export** – Google Earth file showing all aircraft/helicopter routes and waypoints.
- **Multi-mission support** – Process one or more `.miz` files at once.
- **Unit systems** – Choose real-world (imperial) or metric units.

---

## How to Run

1. Download the latest release from the [GitHub Releases page](https://github.com/ScottyMac52/DCSMissionInspector/releases).
2. Extract the zip file anywhere on your computer.
3. Open a command prompt or terminal and navigate to the folder containing the executable (`DcsMissionReader.exe`).
4. Run the program using the command below.

### Command-Line Arguments

You can combine any of the flags below. The program requires at least one export option (`-h`, `-j`, `--full`, or `-k`) to create output files.

| Short | Long                  | Description |
|-------|-----------------------|-------------|
| `-h`  | `--html`              | Generate a full HTML report (`index.html`) in a new folder named after your mission. **This is the most complete and user-friendly output.** |
| `-j`  | `--json`              | Generate a `mission_summary.json` file with high-level mission information. |
|       | `--full`              | Generate a `mission_full.json` file with the complete raw mission data (very large). |
| `-k`  | `--kml`               | Generate a `.kml` file for Google Earth with all routes and waypoints. |
|       | `--metric`            | Use metric units for distances, altitudes, speeds, etc. (default is real-world/imperial units). |

**Positional arguments**: One or more paths to `.miz` files.

### Examples

```bash
# Most common: Create the beautiful HTML report
DcsMissionReader.exe -h "My Mission.miz"

# HTML report + JSON summary + metric units (multiple missions)
DcsMissionReader.exe -h -j --metric "Mission1.miz" "Mission2.miz"

# Everything (HTML + JSON + full data + KML)
DcsMissionReader.exe -h -j --full -k "My Mission.miz"

# Just the KML for Google Earth
DcsMissionReader.exe -k "My Mission.miz"
```

---

## Output Structure

For every `.miz` file you process, the tool creates a new folder named after the mission (e.g. `My Mission Sortie_Report/`). Inside this folder you will find:

- `index.html` – Full HTML report (if `-h` / `--html` was used)
- `mission_summary.json` – High-level summary (if `-j` / `--json` was used)
- `mission_full.json` – Complete raw data (if `--full` was used)
- `My Mission Sortie.kml` – Google Earth file (if `-k` / `--kml` was used)
- `Images/` – All briefing images
- `Kneeboards/` – All kneeboard images and PDFs (original folder structure preserved)

---

## What the Tool Provides

### HTML Report (`index.html`)
This is the main output. It is a single, beautiful web page containing:

- Mission name, map, date, and start time
- Full briefing text (Blue and Red sides)
- Briefing image gallery
- All kneeboards displayed in a clean grid (images and PDFs)
- Player/client slots with aircraft types and roles
- **Flights & Waypoints** – Expandable list for every flight showing:
  - Waypoint table (Action, Altitude, Speed, DCS coordinates, real-world Lat/Lon)
  - Embedded SVG map of the entire route
- Required mods list
- **Air Tasking Order (ATO)** – Summary of every aircraft group, task, aircraft type, quantity, and start time (per coalition)
- Units & Targets – All ground, sea, and static objects
- Weather – Clouds, winds (surface / 2000 ft / 8000 ft), visibility, QNH, temperature
- **Order of Battle (OOB)** – Total counts and breakdowns of aircraft, ships, ground units, and statics per coalition

### JSON Summary (`mission_summary.json`)
High-level mission metadata including name, map, date, briefing text, tasking, and paths to all images and kneeboards.

### Full Export (`mission_full.json`)
The complete mission data in JSON format (everything extracted from the `.miz` file).

### KML File
A Google Earth compatible file showing colored routes for Blue, Red, and Neutral coalitions, with placemarks for every waypoint including name, description, altitude, and speed.

---

**That’s it!**  
Just run the executable with the flags you need and open the generated files. The HTML report is usually all most people want.

Questions or suggestions? Open an issue on the GitHub repository.