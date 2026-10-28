# AR Diagram Tracing System

An Augmented Reality (AR) Diagram Tracing mechanic built for Unity 6 with AR Foundation and XR Interaction Toolkit.

## Key Features

1. **Physical AR Plane Placement & World Locking**
   - The player scans any flat real-world surface (table, desk, floor) with their device camera.
   - Tapping on a detected plane spawns the **Diagram Tracing Board** flat onto the surface.
   - The board is placed rigidly in **3D World Space** (NOT attached to the camera). When the player walks around, tilts, or steps back, the board remains locked to its physical position on the real-world surface.

2. **Snappy Diagram Tracing (Exact Reference Match)**
   - Uses `DiagramTracingCanvas` and `DiagramDataSO` matching the reference project's mechanic.
   - When the player traces along the outline with their finger, ink snaps smoothly to outline curves.
   - Calculates real-time coverage percentage based on length of edges hit.
   - Includes real-time progress bar and percentage display.
   - Includes Pen and Eraser tools, plus a Clear button.

3. **95% Completion Threshold & Submit Button Spawn**
   - The target threshold is set to **95%** (`0.95f`).
   - When `Progress >= 95%`, the **Submit Button** automatically spawns / reveals with visual feedback.
   - The button triggers `onSubmitClicked` UnityEvent for your future submission/transition logic.

4. **Interactive 4-Page AR Biology Book with Object Pooling**
   - **`ARTracingBook`**: Manages 4 two-page spreads (8 diagrams total).
   - **Object Pooling Pattern**: Exactly two persistent `ARTracingBoard` instances (`Left_TracingBoard` and `Right_TracingBoard`) are created inside the book. When flipping pages, **no new prefabs are spawned or destroyed**; instead, each board re-initializes and resets cleanly with the new page's `DiagramDataSO`.
   - **Data Reset on Page Turn**: Turning pages resets the ink canvas, progress bar (to 0%), title, description, and hides the Submit button until the new diagram reaches 95%.
   - **Page Navigation**: Interactive Previous (`◄`) and Next (`►`) arrows with dynamic interactable state at page boundaries (Spread 1 to 4), plus page indicator (`Pages 1–2 of 8`).
   - **Smooth 3D Page Turn Animation**: Animated leaf mesh flips between pages around the spine.

## Page Spreads & 8 Biology Diagrams

- **Spread 1: Cellular Organelles**
  - Left Page: **Mitochondria Organelle** (`Diagram_01_Mitochondria.asset`)
  - Right Page: **Plant Chloroplast** (`Diagram_02_Chloroplast.asset`)
- **Spread 2: Cellular Anatomy**
  - Left Page: **Animal Cell Anatomy** (`Diagram_03_AnimalCell.asset`)
  - Right Page: **Plant Cell Structure** (`Diagram_04_PlantCell.asset`)
- **Spread 3: Human Anatomy & Neurology**
  - Left Page: **Human Heart Anatomy** (`Diagram_05_HumanHeart.asset`)
  - Right Page: **Neuron Nerve Cell** (`Diagram_06_Neuron.asset`)
- **Spread 4: Molecular & Atomic Models**
  - Left Page: **DNA Double Helix** (`Diagram_07_DNAHelix.asset`)
  - Right Page: **Atomic Model (Rutherford)** (`Diagram_08_RutherfordAtom.asset`)

## Quick Start & Usage

1. Open the scene **`Assets/Scenes/ARDiagramTracing.unity`**.
2. Press **Play** in the Unity Editor (XR Simulation environment) or build to Android/iOS.
3. Move the device to scan a flat surface (table/desk) and tap to spawn the **AR Biology Book**.
4. The book is locked firmly in **World Space** with 2 diagrams visible simultaneously (left and right).
5. Trace either diagram with touch/mouse; once a diagram reaches **95%** coverage, its Submit button spawns.
6. Click **Next Page ►** or **◄ Prev Page** to flip through all 4 spreads with pooled board reuse.
