# Unity Simulation - YOLO Testing Environment

A Unity 3D simulation for **testing** trained YOLO models in a realistic underwater robotics environment with real-time inference powered by Unity Sentis.

## Table of Contents
- [Project Summary](#project-summary)
- [Key Features](#key-features)
- [File Structure](#file-structure)
- [System Requirements](#system-requirements)
- [Installation Guide](#installation-guide)
- [Preparing Your YOLO Model](#preparing-your-yolo-model)
- [Setup in Unity](#setup-in-unity)
- [Running and Testing](#running-and-testing)
- [Performance Optimization](#performance-optimization)
- [Troubleshooting](#troubleshooting)
- [Credits](#credits)

## Project Summary

This project is **inspired by** and **adapted from** the [Duke Robotics CV Simulation](https://github.com/DukeRobotics/cv-simulation), originally designed for **automated synthetic dataset generation** for training computer vision models. Shout out to them!

### Key Difference: Training → Testing

**Original Duke Project**: Automated data generation with Unity Perception for creating labeled training datasets (SOLO/COCO format) to train YOLO models.

**This Project**: Real-time model **testing and validation** environment. Instead of generating training data, this simulation allows you to:
- **Test your trained YOLO models** in a realistic underwater environment
- **Validate model performance** with live inference and visual feedback
- **Evaluate detection accuracy** across different camera angles, distances, and lighting conditions
- **Debug model behavior** before deploying to physical robots

The focus has shifted from **dataset creation** to **model evaluation**, using Unity Sentis for in-process YOLO inference without external dependencies.

## Key Features

### Testing & Validation
- ✅ **Real-time Model Testing**: Run trained YOLOv8/YOLOv5/YOLOv11 models directly in Unity
- ✅ **Live Bounding Box Visualization**: See detections overlaid instantly on camera feed
- ✅ **Performance Metrics**: Monitor FPS, inference time, and detection confidence
- ✅ **No External Dependencies**: All inference runs in-process via Unity Sentis

### Realistic Simulation Environment
-  **Underwater Pool Scene**: Realistic swimming pool with HDRP rendering
-  **RoboSub Objects**: Gate and buoy models for competition scenarios
-  **Dynamic Camera Movement**: Automated movements to test various angles and distances
-  **Variable Lighting**: Randomized sun position for diverse lighting conditions
-  **GPU Acceleration**: Leverages Unity Sentis for optimized inference performance

## File Structure

```
RoboSubSimulation/
├── Assets/
│   ├── Scenes/
│   │   └── NewPoolScene/          # Main simulation scene
│   │       ├── Underwater.unity   # Scene file
│   │       └── ...
│   ├── Scripts/
│   │   └── Yolo/                  # YOLO inference system
│   │       ├── YoloSentisClient.cs      # Main inference engine (Unity Sentis)
│   │       ├── YoloFrameStreamer.cs     # Captures camera frames
│   │       ├── YoloBBoxOverlayUGUI.cs   # Draws detection boxes
│   │       ├── YoloTypes.cs             # Data structures for results
│   │       └── IJpegJsonClient.cs       # Interface definition
│   ├── Models/                    # Place your ONNX models here
│   ├── Materials/                 # Pool, gate, buoy materials
│   ├── Textures/                  # Pool and object textures
│   ├── CameraController.cs        # Automated camera movement
│   └── Sun.cs                     # Lighting randomization
├── Python/                        # Optional model conversion utilities
│   ├── requirements.txt           # Python dependencies (for model prep)
│   └── README.md
├── Packages/
│   ├── manifest.json              # Unity package dependencies
│   └── packages-lock.json
├── ProjectSettings/               # Unity project configuration
├── environment.yml                # Conda environment (optional)
└── README.md                      # This file
```

### Key Directories

| Directory | Purpose |
|-----------|---------|
| `Assets/Scenes/NewPoolScene/` | Main simulation scene - open this in Unity |
| `Assets/Scripts/Yolo/` | All YOLO inference code using Unity Sentis |
| `Assets/Models/` | **Place your trained ONNX models here** |
| `Python/` | Optional utilities for model conversion (not needed for simulation) |

## System Requirements

### Hardware
- **CPU**: Multi-core processor (Intel i5/AMD Ryzen 5 or better)
- **GPU**: NVIDIA GTX 1060 / AMD RX 580 or better (for GPU inference)
- **RAM**: 8GB minimum, **16GB recommended**
- **Storage**: 10GB free space

### Software
- **Operating System**: Windows 10/11 (64-bit), macOS 10.15+, or Ubuntu 20.04+
- **Unity**: Unity 2022.3 LTS (or later 2022 LTS version)
- **Graphics API**: DirectX 11/12 (Windows), Metal (macOS), Vulkan (Linux)
- **Python** (Optional): 3.8+ for model conversion utilities only

## Installation Guide

### Step 1: Install Unity Hub and Unity Editor

1. **Download Unity Hub**
   - Visit [unity.com/download](https://unity.com/download)
   - Download and install Unity Hub for your platform

2. **Install Unity 2022 LTS**
   - Open Unity Hub
   - Go to **Installs** tab
   - Click **Install Editor**
   - Select **Unity 2022.3 LTS** (choose the latest patch version)
   
3. **Select Required Modules** (during installation):
   -  **Visual Studio Community** (or use existing IDE)
   -  **Platform Build Support** for your OS:
     - Windows: Windows Build Support (IL2CPP)
     - macOS: Mac Build Support (IL2CPP)
     - Linux: Linux Build Support (IL2CPP)

### Step 2: Clone the Repository

Open a terminal and run:

```bash
git clone <your-repository-url>
cd RoboSubSimulation
```

Or download as ZIP and extract to your desired location.

### Step 3: Open Project in Unity

1. Launch **Unity Hub**
2. Click **Open** (or **Add** → **Add project from disk**)
3. Navigate to and select the `RoboSubSimulation` folder
4. Click **Select Folder**
5. Wait for Unity to import assets (**first time: 5-15 minutes**)
   - Unity will download and install required packages
   - HDRP shaders will compile
   - Scripts will compile

> [!NOTE]
> If prompted about Unity version mismatch, click **Continue** to open with your installed version. Minor version differences are usually compatible.

### Step 4: Verify Package Installation

After opening the project:

1. Go to **Window** → **Package Manager**
2. Verify these packages are installed:
   - ✅ **High Definition RP** (v14.0.12 or later)
   - ✅ **Unity Sentis** (v2.1.0 or later) - *Critical for YOLO inference*
   - ✅ **TextMesh Pro** (v3.0.7 or later)
   - ✅ **Unity UI** (v1.0.0 or later)

If **Unity Sentis** is missing:
1. In Package Manager, click **+** (top-left)
2. Select **Add package by name**
3. Enter: `com.unity.sentis`
4. Click **Add**

### Step 5: Python Setup (Optional - For Model Conversion Only)

> [!IMPORTANT]
> **Python is NOT required to run the simulation.** Only install Python if you need to convert models from PyTorch to ONNX format.

<details>
<summary><b>Click to expand Python setup instructions</b></summary>

**Option A: Using Conda (Recommended)**
```bash
# Install Miniconda from https://docs.conda.io/projects/miniconda/
conda env create -f environment.yml
conda activate cv-sim
```

**Option B: Using pip + venv**
```bash
cd Python
python -m venv .venv

# Activate virtual environment:
# Windows:
.venv\Scripts\activate
# macOS/Linux:
source .venv/bin/activate

pip install -r requirements.txt
```

</details>

---

## Preparing Your YOLO Model

You need a trained YOLO model in **ONNX format** to use with Unity Sentis. Here are the two main workflows:

### Option 1: Export from Roboflow (Recommended for Beginners)

Roboflow provides trained models that can be directly exported to ONNX format.

#### 1.1 Train Your Model on Roboflow

1. Go to [Roboflow](https://roboflow.com) and create an account
2. Create a new project and upload your training images
3. Annotate your objects (gate, buoy, etc.) with bounding boxes
4. Train a YOLOv8 model using Roboflow's training service

#### 1.2 Export as YOLOv8 ONNX

1. In your Roboflow project, go to **Versions** → Select your trained model version
2. Click **Export**
3. Select **Format**: **ONNX**
4. Choose **YOLOv8** as the architecture
5. Click **Download** or **Get Link**
6. Extract the downloaded ZIP file

You'll get:
- `model.onnx` - Your ONNX model file
- `classes.txt` - Class names (one per line)
- Metadata files

#### 1.3 Convert to Unity Sentis Format (if needed)

Unity Sentis can import ONNX directly, but sometimes requires optimization:

```bash
# Activate Python environment first
pip install onnx onnxsim

# Simplify ONNX model for better compatibility
python -m onnxsim model.onnx model_simplified.onnx
```

### Option 2: Export from Local PyTorch/Ultralytics Model

If you trained a model locally using Ultralytics YOLOv8:

#### 2.1 Install Ultralytics

```bash
pip install ultralytics
```

#### 2.2 Export to ONNX

```bash
# Export your trained .pt model to ONNX
yolo export model=path/to/your/best.pt format=onnx imgsz=640 simplify=True
```

Parameters:
- `model`: Path to your trained `.pt` file (e.g., `runs/detect/train/weights/best.pt`)
- `format=onnx`: Output format for Unity Sentis
- `imgsz=640`: Input size (640x640 is standard, adjust if needed)
- `simplify=True`: Optimize the ONNX graph

Output: `best.onnx` in the same directory as your `.pt` file

#### 2.3 Verify ONNX Model

```bash
pip install onnx

python -c "import onnx; model = onnx.load('best.onnx'); print(onnx.checker.check_model(model, full_check=True))"
```

### Option 3: Convert Pre-trained Ultralytics Models

To use official Ultralytics pre-trained models:

```bash
# Download and export a pre-trained model
yolo export model=yolov8n.pt format=onnx imgsz=640 simplify=True
# Available models: yolov8n, yolov8s, yolov8m, yolov8l, yolov8x
```

### Import ONNX Model to Unity

1. **Copy your ONNX file** to `Assets/Models/` in Unity project
2. Unity will automatically import it as a **ModelAsset**
3. In Unity Editor, select the imported `.onnx` file
4. In Inspector, verify it shows as **Sentis Model Asset**


### Create Labels File

Create a text file `labels.txt` with your class names (one per line):

```text
gate
buoy
path_marker
torpedo_target
```

Save in `Assets/Models/` and Unity will import it as a **TextAsset**.

---

## Setup in Unity
Quick wiring to connect the supplied YOLO scripts to a Camera and UI overlay.

- Create an empty GameObject and add the `YoloSentisClient` component. Set `Model Asset` to your imported ONNX, `Labels` to `labels.txt`, `Input Size` to your model input (e.g., 640), `Assume Letterboxed` = true, and set `Score Threshold`/`IOU Threshold` as desired.

- On the Camera you want to use for detection, add `YoloFrameStreamer` and set `Width` (e.g., 640), enable `Auto Match Screen Aspect`, set `Capture Fps` (e.g., 12–15), and assign the `YoloSentisClient` to `Sentis Client`.

- Create a `Canvas` (UI → Canvas). Under it add an empty UI GameObject and attach `YoloBBoxOverlayUGUI`. Assign the `YoloSentisClient` to the overlay and ensure the Canvas Scaler is set to `Scale With Screen Size` (reference resolution e.g., 1920x1080).

- Important: set the Game view aspect ratio to 16:9 (for example 1920x1080). The overlay mapping is tuned for a widescreen capture; using 16:9 ensures correct box alignment.

- Wiring summary: Camera (`YoloFrameStreamer`) → `YoloSentisClient`; Canvas child (`YoloBBoxOverlayUGUI`) → `YoloSentisClient`.

- Play & verify: press Play and check the Console for `[YoloSentisClient] First frame received` and `dets=` logs. If boxes are misaligned, try `Screen Space - Camera` on the Canvas or enable `PreserveAspect` in the overlay.


---

## Running and Testing

### Start the Simulation

1. **Click the Play button** (▶) at the top-center of Unity Editor
2. The **Game** view will activate showing the camera output
3. Watch for:
   - Camera rendering the underwater scene
   - YOLO inference starting (check Console for logs if enabled)
   - Bounding boxes appearing over detected objects

### What You Should See

- **Live camera feed** in the Game view showing the pool environment
- **Colored bounding boxes** around detected objects (gate, buoy, etc.)
- **Class labels and confidence scores** above each detection (if enabled)
- **Real-time updates** as camera moves and objects are detected

### Monitoring Performance

#### Console Output
- Open: **Window → General → Console**
- Look for:
  - `[YoloSentisClient] Model loaded` - Confirms model initialization
  - Inference time messages (if logging enabled)
  - Any errors or warnings

#### Stats Panel
- Open: **Window → Analysis → Stats**
- Monitor:
  - **FPS**: Frame rate (aim for 30+ for smooth testing)
  - **Frame Time**: Time per frame (lower is better)
  - **Batch Count**: Graphics performance

### Controls During Testing

| Action | Method |
|--------|--------|
| **Pause simulation** | Click Play button (▶) again |
| **Resume** | Click Play button once more |
| **Stop completely** | Click Play button while paused (or stop square ■ button) |
| **Step one frame** | Click Pause, then Step button (▶\|) |
| **Adjust camera view** | Use Scene view (doesn't affect detection) |

### Testing Workflow

1. **Initial Test Run**
   - Start simulation
   - Verify detections appear
   - Check Console for errors
   - Note FPS and performance

2. **Adjust Detection Threshold**
   - Stop simulation
   - Modify **Score Threshold** in YoloSentisClient
   - Restart and observe changes

3. **Test Different Scenarios**
   - Let camera move to different positions
   - Observe detection consistency
   - Note any false positives/negatives
   - Check detection confidence scores

4. **Performance Tuning**
   - If FPS is low, try:
     - Switching Backend to CPU
     - Reducing Game view resolution
     - Using smaller YOLO model (yolov8n instead of yolov8m)

### Interpreting Results

**Good Model Performance:**
-  Consistent detections across different camera angles
-  High confidence scores (>0.7) for correct detections
-  Stable bounding boxes (minimal jitter)
-  Few or no false positives
-  Maintains 30+ FPS

**Signs of Issues:**
-  No detections appearing → Check model assignment, labels, threshold
-  Many false positives → Increase score threshold
-  Missing objects in frame → Lower score threshold or retrain model
-  Flickering boxes → Adjust IOU threshold
-  Low FPS → Optimize backend or use smaller model

### Exporting Test Results

To capture screenshots of detections:
1. While simulation is running, press **F12** (or Game view menu → Screenshot)
2. Images save to project root by default
3. Use for documentation or presentation

---

## Performance Optimization

### If Experiencing Low FPS

1. **Switch Backend**
   ```
   YoloSentisClient → Backend → Try CPU (or GPUCompute)
   ```

2. **Reduce Game View Resolution**
   - Click Game view dropdown → Select lower resolution
   - Or set to "Free Aspect" and resize window smaller

3. **Use Smaller Model**
   - Replace with YOLOv8n instead of YOLOv8m/l/x
   - Smaller model = faster inference, slightly less accuracy

4. **Lower Graphics Quality**
   - **Edit → Project Settings → Quality**
   - Select a lower preset (Low or Medium)

5. **Reduce Frame Rate**
   ```
   YoloFrameStreamer → Target Frame Rate → Set to 15 or 20
   ```

### Optimizing Detection Quality

**Too Many False Positives:**
- Increase **Score Threshold** (e.g., from 0.25 to 0.50)
- Increase **IOU Threshold** (e.g., from 0.45 to 0.60)

**Missing Real Objects:**
- Decrease **Score Threshold** (e.g., from 0.50 to 0.25)
- Verify model was trained on similar objects/conditions

**Flickering Detections:**
- Adjust **IOU Threshold** (try 0.30 or 0.60)
- Consider temporal smoothing (requires code modification)

---

## Troubleshooting


### Common Issues and Solutions

| Issue | Possible Cause | Solution |
|-------|---------------|----------|
| **Black screen in Game view** | HDRP not configured | **Edit** → **Project Settings** → **HDRP** → **Default Settings** → Verify settings |
| **No bounding boxes appear** | Model not loaded or no detections | 1. Check model is assigned<br>2. Lower Score Threshold<br>3. Check Console for errors |
| **"Sentis not found" error** | Unity Sentis package missing | **Window** → **Package Manager** → Add `com.unity.sentis` |
| **Pink/magenta materials** | HDRP shader issue | **Edit** → **Render Pipeline** → **HDRP** → **Upgrade Materials** |
| **Console errors about model shape** | ONNX model incompatible | Re-export with `simplify=True` or use onnxsim |
| **Very low FPS (<10)** | Heavy model or slow GPU | Switch to CPU backend or use smaller model (yolov8n) |
| **"Model Asset is null"** | ONNX not assigned | Drag `.onnx` file to Model Asset field in Inspector |
| **Labels not showing** | Labels file not assigned or mismatch | 1. Assign labels.txt file<br>2. Verify class count matches model |
| **Crash on Play** | GPU driver issue or memory | Update GPU drivers, or switch to CPU backend |

### Getting Help

If you encounter issues:

1. **Check Console**: Look for error messages
2. **Verify Setup**: Ensure all steps in "Setup in Unity" were followed
3. **Test with Sample Model**: Try a small pre-trained YOLOv8n model first
4. **Check Unity Version**: Ensure using Unity 2022.3 LTS or later
5. **GPU Compatibility**: Verify your GPU supports compute shaders

---

## Additional Resources

### Documentation
- [Unity Sentis Documentation](https://docs.unity3d.com/Packages/com.unity.sentis@latest) - Official Sentis API and guides
- [YOLOv8 Ultralytics Docs](https://docs.ultralytics.com/) - YOLO training and export
- [HDRP Manual](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@latest) - Render pipeline details
- [Unity Scripting Reference](https://docs.unity3d.com/ScriptReference/) - C# API documentation

### Tutorials
- [Unity Sentis Getting Started](https://docs.unity3d.com/Packages/com.unity.sentis@latest/manual/get-started.html)
- [Roboflow YOLO Training](https://blog.roboflow.com/train-yolov8-object-detection/)
- [ONNX Model Export Guide](https://docs.ultralytics.com/modes/export/)

### Community
- [Unity Forums - Sentis](https://forum.unity.com/forums/sentis.672/)
- [Ultralytics GitHub Discussions](https://github.com/ultralytics/ultralytics/discussions)

---

## Credits

### Acknowledgments

This project is **inspired by** and **adapted from**:
- **[Duke Robotics CV Simulation](https://github.com/DukeRobotics/cv-simulation)** - Original project for automated synthetic dataset generation using Unity Perception

### Key Modifications

- **Shifted focus** from training data generation to model testing and validation
- **Replaced Unity Perception** with Unity Sentis for real-time in-process inference
- **Added live visualization** with bounding box overlay system
- **Optimized for real-time performance** with GPU-accelerated inference

### Technologies Used

- **Unity 2022 LTS** - Game engine and simulation environment
- **Unity Sentis** - Neural network inference runtime
- **High Definition Render Pipeline (HDRP)** - Advanced rendering
- **YOLOv8/Ultralytics** - Object detection models
- **ONNX** - Model interchange format
