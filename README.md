# 🚀 DEPO Missile Mod

## What is this?

A networking mod for the Unity-based game **DEPO**, enabling real-time missile synchronization between multiple players over a **UDP/HTTPS server**.  
It supports **launching**, **movement tracking** and **skin selection** of missiles.

---

## 🔧 Features

- 🔄 **Cross-player missile visibility** — see rockets launched by other players in real-time  
- 📡 **Server sync via UDP/HTTPS** (hybrid polling)  
- 🚀 **Missile skins** mapped dynamically to visual prefab assets  
- 🧠 **Smart state handling**:  
  - Missiles spawn, move, and explode based on server state  
  - Automatic despawn if missile is no longer tracked by the server  
- 🛡️ Built using **Harmony** and **BepInEx**

---

## 🛠 Requirements

1. **BepInEx** 5.4+
2. **.NET Framework** 4.7+
3. **DEPO** game 

---

## 🛠️ How to Install

1. **Install DEPO: Death Epileptic Pixel Origins**  
   Download and install the game from Steam:  
   👉 https://store.steampowered.com/app/1091320/

2. **Install BepInEx**  
   Download the latest version of **BepInEx (x64)** from the official repository:  
   👉 https://github.com/BepInEx/BepInEx  
   Then, extract the contents into the **root folder** of the game (where the `.exe` file is located).

3. **Install the Mod Plugin**  
   - Download the `.dll` file from the [Releases](../../releases) section of this GitHub repository.  
   - Move the `.dll` file to the `BepInEx/plugins` folder inside the game directory.  
     > 🔸 If the `plugins` folder does not exist, create it manually.

4. **Launch the Game**  
   Run DEPO through Steam as usual. If everything is installed correctly, the mod will be loaded automatically by BepInEx.

---

🧪 **Work in progress** – under active development, with planned features like **performance boosts** and **custom explosion effects**.
