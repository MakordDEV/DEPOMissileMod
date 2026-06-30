# DEPO Missile Mod

![Downloads](https://img.shields.io/github/downloads/makorddev/DEPOVoiceChat/total?style=for-the-badge)
![License](https://img.shields.io/github/license/makorddev/DEPOVoiceChat?style=for-the-badge)
![Unity](https://img.shields.io/badge/Unity-2021%2B-black?logo=unity&style=for-the-badge)
![BepInEx](https://img.shields.io/badge/BepInEx-5.4%2B-blue?style=for-the-badge)

## What is this?

Networking mod for the Unity game DEPO. It adds real-time missile synchronization between players using a custom UDP/HTTPS server.  
You can launch missiles, see other players' rockets in real time, and choose different missile skins.

---

## Features

- Cross-player missile visibility — see rockets launched by others instantly
- Server synchronization via UDP/HTTPS (hybrid approach)
- Dynamic missile skins mapped to visual prefabs
- Smart state handling: missiles spawn, move, explode and despawn correctly

---

## Requirements

- BepInEx 5.4+
- .NET Framework 4.7+
- DEPO: Death Epileptic Pixel Origins from Steam

---

## Installation

1. Install the game
   Download DEPO from Steam: [https://store.steampowered.com/app/1091320/](https://store.steampowered.com/app/1091320/)

2. Install BepInEx 
   Download the latest BepInEx 5 (x64) from [the official repository](https://github.com/BepInEx/BepInEx) and extract it into the game's root folder (next to `DEPO.exe`).

3. Install the mod
   - Download the `.dll` from the [Releases](../../releases) section.
   - Place the `.dll` file into the `BepInEx/plugins/` folder (create it if it doesn't exist).

4. Launch the game
   Start DEPO through Steam. The mod should load automatically.

---

## Status

Work in progress after a year's absence, although without such ambitions and without such activity. Currently focused on stability and sync quality.

---

## Contributing

Issues, suggestions and pull requests are welcome. 

If you run into crashes, send the log from:  
`%USERPROFILE%\AppData\LocalLow\6 Faces Team\DEPO\player.log`

Feel free to reach out on Discord/X: **makordikrom**
