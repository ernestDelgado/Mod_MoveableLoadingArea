Movable Loading Area (Supermarket Simulator Mod)

A quality-of-life mod that allows players to move the delivery/loading zone anywhere in the world, Mainly to bring it inside
and speed up the stocking process. 

🚀 Features
Press H to enter move mode
Move the delivery zone using your crosshair
Scroll wheel rotates the zone (45° increments)
Left click to confirm placement
Placement is clamped to valid floor surfaces (store, sidewalk, etc.)
Position is saved per save file
Fully integrates with the game’s existing delivery system

🧠 Technical Overview
This project was built using IL2CPP reverse engineering and Harmony patching.

Key Implementations
Hooked into DeliveryManager.Delivery() to apply placement before deliveries spawn
Used the actual SortableBoxManager transform to control real delivery behavior
Implemented a center-screen raycast system for intuitive placement
Built a floor validation system to prevent invalid placements
Added rotation snapping (45° steps) for consistent alignment
Synced visual elements (indicator + text) with actual ground height
Implemented per-save persistence using custom file storage

🔍 Reverse Engineering Workflow
To understand and control the delivery system:
Used Il2CppInspectorRedux to extract:
class structures
field relationships
metadata
Analyzed native logic using Ghidra
Traced delivery flow:
DeliveryManager → SortableBoxManager → World Spawn
Identified that SortableBoxManager.transform governs:
delivery spawn position
loading zone visuals
interaction area
Confirmed that modifying this transform safely redirects all deliveries

⚙️ Installation
Install BepInEx (IL2CPP version)
Place the compiled .dll into:
BepInEx/Plugins/

📌 Notes
This mod focuses on:

Extending gameplay without breaking vanilla systems
Maintaining immersion and realism
Using existing game mechanics instead of replacing them
Clean, deterministic control of delivery behavior

👤 Author
Ernest Delgado (yaboie88)
Brooklyn, NY

⭐ Support
If you enjoy the mod:

Leave a kudos / endorsement
Share feedback or suggestions

It helps support continued development and future projects

🔗 Links
https://github.com/ernestDelgado/MoveableLoadingArea
https://www.nexusmods.com/supermarketsimulator/mods/1483
