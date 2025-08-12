# CodinGame Challenge Solutions

This repository contains my solutions to various **CodinGame** challenges, covering both classic puzzles and competitive optimization problems.  
Each folder contains a self-contained implementation for a specific game or contest.  
Most solutions are written in **C#** or **C++**, with some helper scripts in **Python** for visualization or analysis.

## Highlights

- **Algorithmic diversity:** Implementations include Monte Carlo Tree Search (MCTS), Genetic Algorithms (GA/SGA), Simulated Annealing (SA), greedy heuristics, and A* pathfinding.
- **Performance-aware design:** Profiling and optimization techniques are applied to meet CodinGame's strict time limits (e.g., custom timers, efficient memory management).
- **Competitive results:**  
  - **Constrained Vehicle Routing** – 28th / 400 (SGA)  
  - **AStarCraft** – 126th / 2000 (Simulated Annealing)  
  - **Mars Lander** – 200th / 8100 (Genetic Algorithm)  
  - **Bandas** – 51st / 570 (MCTS)

## Project Overview

| Challenge                                | Language        | Summary                                                                                                                                                                                                  | Key Algorithms / Techniques                                                      |
|------------------------------------------|----------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------|
| **Bandas**                               | C#              | Strategy game controlling teams on a grid. Uses MCTS with a custom board representation and tuned heuristics. Includes a Python script (`mcts_visualizer.py`) for tree visualization. Ranked **51/570**. | Monte Carlo Tree Search; game state cloning; custom performance timers            |
| **Ghost in the Cell**                    | C++             | Factory warfare: balance troop movement, upgrades, and bombs. Greedy agent prioritizes reinforcements, upgrades, and attacks based on enemy movements and factory production.                           | Greedy heuristics; distance-based targeting; bomb logic                           |
| **Gargoyles**                            | C#              | Arcade-style game where gargoyles catch falling gifts and shoot fireballs. Predicts item trajectories, evaluates enemy reach, and decides between movement or attack.                                   | Real-time heuristics; geometry & kinematics; opponent prediction                  |
| **Mars Lander (GA)**                     | C++             | Landing simulation using a genetic algorithm: each chromosome encodes a command sequence, evaluated by simulating flight physics to ensure a safe landing. Ranked **200/8100**.                         | Genetic Algorithm; physics simulation; mutation/crossover; tournament selection   |
| **Lazzie Come Home**                     | C#              | Navigation puzzle with limited visibility. Uses A* and dynamically replans routes as new obstacles are discovered.                                                                                      | A* search; map exploration; priority queues                                       |
| **Landmarks Pathfinding**                | C#              | Landmark placement optimization to speed up shortest path queries in large graphs. Splits map into components, assigns landmarks proportionally, and picks farthest nodes via Dijkstra.                  | Connected components analysis; landmark selection; Dijkstra                       |
| **AStarCraft** *(Optimization Problem)*  | C#              | Multi-agent pathfinding and survival optimization on a 19×10 board with platforms and arrows. Uses **Simulated Annealing** to coordinate agents, avoid cycles, and maximize survival time. Ranked **126/2000** submissions. | Simulated Annealing; bitboard representation; multi-agent control; cycle detection |
| **Constrained Vehicle Routing** (SGA)    | C#              | Time-constrained vehicle routing optimization. Uses a **Simulated Genetic Algorithm**: initial routes built with a greedy heuristic, then refined via crossover and mutation with top-performer selection. Ranked **28/400**. | Simulated Genetic Algorithm; greedy initialization; crossover/mutation; route scoring |

## Running the Solutions

Each challenge directory is self-contained and can be built/run independently.

- **C# projects**: Include `.sln` and `.csproj` files for direct compilation.
- **C++ projects**: Can be compiled with any modern C++17 compiler.
- **Python scripts**: Provide visualization or analysis tools, not game logic.

---

For more details, check out my [CodinGame profile](https://www.codingame.com/profile).
