# Guild Wars 2 Character Map Tracker

A Blish HUD module for tracking map completion progress **per character** in Guild Wars 2. This addon provides both a live tracker and a comprehensive map progress window, helping you keep track of every waypoint, landmark, vista, hero point, and heart as you explore Tyria.

---

## Features

### 🧭 Live Tracker Window
- **Shows your current coordinates**.
- **Displays the nearest Point of Interest (POI)** (waypoint, landmark, vista, hero point, or heart) and its distance (for hearts it appears whenever inside the boundary, meaning where it can be completed).
- **Automatic completion:**  
  - Waypoints, Landmarks, Vistas, and Hero Points are automatically marked as found when you are within 15 units of them.
- **Manual completion for Hearts:**  
  - Hearts must be checked manually when you finish them.
  - The heart boundary is shown in the live tracker for easy reference.
- **Works per character:**  
  - Each character has their own progress tracked and saved.

### 🗺️ Map Progress Window
- **Browse all maps by Expansion/DLC and Region.**
- **See your completion progress for each map:**  
  - Objectives are grouped and counted (Waypoints, Landmarks, Vistas, Hero Points, Hearts).
  - Each map shows which objectives have been discovered and which remain.
- **Click a map to see detailed progress:**  
  - View a breakdown of all objectives, with checkboxes for each (for now the checkboxes only show if the point of interest has been discovered or not, meaning the discovered status cannot be changed yet).


---

## How Progress Tracking Works

- **Progress is tracked from scratch** for each character when the addon is first activated.
- **Existing map completion is not imported** (yet):  
  - Even if your character has already explored parts of the map, the tracker will start at 0% and you will need to re-explore objectives for them to be marked as found, as of now the implementation works best for new character, but this will be improved soon.
- **All progress is saved per character** and persists between sessions.

---

## Pathing Integration & Credits

This module works **best when used alongside [Tekkit's Pathing Module](https://www.tekkitsworkshop.net/index.php/markers/blishhud-markers-installation)** for Blish HUD.  
It uses the coordinates and marker data provided by Tekkit's Pathing system to accurately track and display map objectives (mainly because the game's API does not provide Z coordinates, meaning it does not distinguish between floors in the map).  
**Special thanks and credit to Tekkit and the Pathing module contributors** for their work on the pathing system.

---

## Planned / Future Features

- **Import existing map completion:**  
  - Ability to import already discovered/completed objectives for other existing characters.
- **Improved UI/UX and customization options.**
  - "Show on Map" option to highlight a POI on the world map to easily distinguish where each objective is located within the map.
  - Save existing progress prior to using the module from the UI (for existing characters with already exploration progress).
---

## Installation


1. Download the latest release from the [Releases](https://github.com/tiago-ga/CharacterMapTracker/releases) page.
2. Place the `.bhm` file in your `Blish HUD\modules` directory.
3. Launch Blish HUD and enable the Character Map Tracker module.
(Since it is still a work in progress, it will need to be manually added, I will add it to he public modules that can be downloaded within Blis hHUD in the near future)
---

## Feedback & Contributions

- Issues and suggestions are welcome!
- Please report bugs or feature requests via the [Issues](https://github.com/tiago-ga/CharacterMapTracker/issues) page.

---

**Enjoy tracking your map completion, one character at a time!**