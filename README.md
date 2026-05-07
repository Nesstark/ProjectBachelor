# Ludwig: Uncrumbled

## Academic Context

**Thesis question:**
> *"???"*

This project was developed as part of a Bachelor's degree in Medialogy at Aalborg University in Copenhagen and serves as both a playable game and a research artefact.

---

## Built With

- **Engine:** Unity 6.3 LTS `(6000.3.7f1)`
- **Input System:** Unity New Input System (no legacy input)
- **Rendering:** Universal Render Pipeline (URP)
- **Physics / Navigation:** Unity NavMesh
- **Biofeedback:** [open-rppg](https://github.com/fanteck-boop/open-rppg) — camera-based heart rate & HRV via Python

---

## Getting Started

### Prerequisites

- [Unity Hub](https://unity.com/download) installed
- Unity Editor version **6000.3.7f1** (Unity 6.3 LTS) — install via Unity Hub
- **Python 3.9–3.13** installed
- Git installed on your machine
- A webcam (required for the rPPG baseline calibration)

---

### 1. Clone the Repository

```bash
git clone https://github.com/Nesstark/ProjectBachelor.git
cd ProjectBachelor
```

---

### 2. Set Up the rPPG Python Server

The game communicates with a local Python process that handles webcam-based heart rate reading via [open-rppg](https://github.com/fanteck-boop/open-rppg).

**Install the package:**

```bash
pip install open-rppg
```

> For GPU acceleration on Linux/CUDA, also run:
> ```bash
> pip install jax[cuda]
> ```

**Start the rPPG server before launching the game:**

```bash
python rppg_server.py
```

> The server must be running **before** you press Play in Unity. It streams heart rate, HRV, and signal quality data to the game over a local socket. Keep this terminal window open while playing.

---

### 3. Open in Unity

1. Open **Unity Hub**
2. Click **"Open"** → **"Add project from disk"**
3. Navigate to the cloned `ProjectBachelor` folder and select it
4. Ensure Unity Hub uses editor version **6000.3.7f1** for this project
5. Let Unity import all assets (may take a few minutes on first open)

---

### 4. Running the Game

1. Make sure the **rPPG Python server is running** (see step 2)
2. In the Unity Editor, open the **`MainMenu`** scene from `Assets/Scenes/`
3. Press the **Play** button
4. From the Main Menu:
   - **New Game** — start a fresh run (available if no save file exists)
   - **Continue** — resume from your last save (available if a save file exists)
5. **Every time you launch the game**, a **120-second rPPG baseline calibration** runs in the start room before play begins — sit in front of your webcam and remain relatively still until the doors unlock

> **Note:** Calibration runs on every fresh launch of the application. If you close and reopen the game, a new 120-second calibration is required. This is by design — rPPG baselines are session-specific and cannot carry over between sessions.


---

## Group Members

- Christoffer Bromberg Thomsen
- Emil Miliam Madsen
- Oliver Hee Mortensen
- Tristan Samuel Alanne

---

## License

This project is licensed under the terms found in [`LICENSE`](./LICENSE).

---

<p align="center">Made with crumpled paper and a tiny knife 🗡️</p>