# AStarVisual — A* Pathfinding Visualizer ( .NET MAUI, .NET 8/9 )

Visual, interactive A* pathfinding on a grid. Paint walls, set **Start**/**Goal**, toggle **Diagonal** moves, and watch the frontier expand step-by-step with animation, speed control, and optional `f/g/h` overlays.

---

## ✨ Features
- Interactive grid editor: paint **Walls**, **Erase**, set **Start** (green) and **Goal** (red)
- Animated **A\***: **Open/frontier** (light blue), **Closed/explored** (slate), and final **Path** (gold)
- Speed slider and **Stop** button
- **Diagonal** toggle with **octile** heuristic (√2 diagonals)
- Optional per-cell **f/g/h** overlay
- Targets **Windows** and **Android** via .NET MAUI

---

## 🚀 Quick Start

### Requirements
- .NET SDK 8 or 9
- Visual Studio 2022 with **.NET MAUI** workload (or `dotnet workload install maui`)
- Windows 10/11 for WinUI target (and/or an Android emulator)

### Run
**Visual Studio:** open the solution → select **Windows Machine** (or an Android emulator) → **Run**.

**CLI (Windows):**
```bash
dotnet build -t:Run -f net8.0-windows10.0.19041.0
# or (if configured):
# dotnet build -t:Run -f net9.0-windows10.0.19041.0
```

---

## 🧭 How to Use
1. Choose mode: **Wall**, **Erase**, **Start**, **Goal**
2. Click/drag on the grid to edit
3. Toggle **Diagonal** for 8-direction moves
4. (Optional) enable **Animate** and adjust **Speed**
5. Click **Run A\*** to search
   - **Gold** path appears when goal is reached
   - Label shows **Steps**, **g(n)**, and **expanded** nodes
6. **Stop** cancels, **Clear** resets, **Random** generates a new map

**Colors**
- Dark gray = walls
- White = free cell
- Green = start, Red = goal
- Light blue = open/frontier
- Slate = closed/explored
- Gold = final path

---

## 🧠 Algorithm (A*)
Minimize **f(n) = g(n) + h(n)**  
- **g(n)**: path cost (1 orthogonal, √2 diagonal)  
- **h(n)**: heuristic — **Manhattan** (4-way) or **Octile** (8-way)  
Admissible & consistent → **optimal shortest path**.

**Structures**
- `PriorityQueue<Cell,double>` → open set by lowest **f**
- `HashSet<Cell>` → open/closed/path (for drawing)
- `Dictionary<Cell,double>` → best **g**
- `Dictionary<Cell,Cell>` → `came_from` for path reconstruction

Complexity: ~**O(E log V)**.

---

## 🧩 Files
```
App.xaml              # XAML resources, App class
App.xaml.cs           # overrides CreateWindow() → new AStarPage()
MauiProgram.cs        # MAUI host builder
AStarPage.xaml        # UI: GraphicsView + controls
AStarPage.xaml.cs     # interaction, animation, A* logic hooks
Platforms/            # platform-specific targets (Windows/Android/etc.)
```

> On .NET 8/9 we use `CreateWindow(...)` instead of setting `MainPage` directly.

---

## 🛠 Troubleshooting
- **XAML 'x' undeclared** → ensure `xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"` in `App.xaml`
- **Obsolete: Application.MainPage.set** → use `CreateWindow` in `App.xaml.cs`
- **Event hookup** → use `+=` (not `==`) e.g., `Canvas.StartInteraction += (s,e) => HandleTouch(e);`
- **Touches Count error** → use LINQ: `if (!e.Touches.Any()) return; var p = e.Touches.First();`
- **Namespace mismatch** → `x:Class` and code-behind `namespace` must match (e.g., `AStarVisual`)

---
##  Video Demonstration
- **Google Drive link:**
`https://drive.google.com/drive/folders/1Ktqpipst4RQ8gGXSmZkd-mVuVjZRFg4E?usp=sharing`

