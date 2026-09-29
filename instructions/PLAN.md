# Implementation Plan and Roadmap (PLAN.md)

This document tracks the technical roadmap, progress, and acceptance criteria for the ATCsim (Air Traffic Control Simulator) project across all 10 engineering stages.

Any LLM or developer working on this codebase must consult this document before beginning a task and update checkbox states (`[ ]` -> `[x]`) upon verification.

---

## 1. Module Ownership & Team Responsibilities

- **Oleksandr Bezkorovainyi** (Lead / Architecture): Core simulation engine (`ATCsim.Core`), kinematics algorithms, collision detection, solution coordination.
- **Maksym Borkov** (Core / Data): Deterministic seed-based procedural world generation (`WorldGenerator`), database layer (`ATCsim.Data`, SQLite), session persistence.
- **Vladyslava Ruban** (QA / CI/CD): Automated test suite (`ATCsim.Tests`), GitHub Actions CI/CD workflows, security validation, documentation tracking.

---

## 2. Stage Summary & Status

| Stage | Description | Status | Reference / Artifacts | Owner |
|:---:|---|:---:|---|---|
| **1** | Project Initialization & Topic Selection | **[x] Completed** | [docs/Report_task_1.md](docs/Report_task_1.md) | All |
| **2** | Requirements Engineering & Scope (No MoSCoW) | **[x] Completed** | [docs/Report_task_2.md](docs/Report_task_2.md) | All |
| **3** | Domain Modeling & UI Design | **[x] Completed** | [docs/Report_task_3.md](docs/Report_task_3.md) | O. Bezkorovainyi, M. Borkov |
| **4** | Architecture & Relational Data Model | **[x] Completed** | [docs/Report_04.md](docs/Report_04.md) | O. Bezkorovainyi, M. Borkov |
| **5** | Solution Decomposition & MVP Baseline | **[ ] In Progress** | Initial working prototype | O. Bezkorovainyi, M. Borkov |
| **6** | Iterative Feature Implementation | **[ ] Planned** | Feature branches & PRs | All |
| **7** | Automated Testing & Quality Assurance | **[ ] Planned** | Unit & integration test suites | V. Ruban |
| **8** | Continuous Integration (CI/CD) | **[ ] Planned** | GitHub Actions workflows | V. Ruban |
| **9** | Logging, Exception Handling & Security Audit | **[ ] Planned** | Structured logging, sanitizers | M. Borkov, V. Ruban |
| **10** | Release Finalization & Project Defense | **[ ] Planned** | Final demo & technical review | All |

---

## 3. Detailed Stage Breakdown

### Stage 1: Project Initialization & Topic Selection [COMPLETED]
- [x] Form team and establish engineering areas of responsibility.
- [x] Define problem statement, target audience, and primary objectives for ATCsim.
- [x] Establish technical stack: C# 12+, .NET 10, WPF (XAML), SQLite.
- [x] Initialize public GitHub repository ([dyxell1n/ATCsim](https://github.com/dyxell1n/ATCsim)).
- [x] Document and finalize [docs/Report_task_1.md](docs/Report_task_1.md).

---

### Stage 2: Requirements Engineering & Scope [COMPLETED]
- [x] Identify stakeholders: air traffic controller, trainee, instructor, system analyst.
- [x] Conduct competitive analysis against existing simulators (OpenATC, FlightGear, Radar Contact).
- [x] Formulate functional requirements (FR 1 to FR 8) and non-functional requirements (NFR 1 to NFR 3).
- [x] Author 5 key User Stories with testable Acceptance Criteria (AC).
- [x] Establish MVP boundaries and tiered backlog (strictly excluding MoSCoW method).
- [x] Document and finalize [docs/Report_task_2.md](docs/Report_task_2.md).

---

### Stage 3: Domain Modeling & UI Design [COMPLETED]
- [x] Model system Use Cases across 3 actors: Controller, Instructor, Simulation Engine.
- [x] Specify 8 operational interaction scenarios.
- [x] Design conceptual relational entity model.
- [x] Construct Swiss Minimalist desktop UI specification (white palette, Montserrat, black vector SVG plane icons).
- [x] Define interaction flows between UI, computation loop, and data persistence.
- [x] Document and finalize [docs/Report_task_3.md](docs/Report_task_3.md).

---

### Stage 4: Architecture & Relational Data Model [COMPLETED]
- [x] Select architectural pattern: Layered Architecture + MVVM + Event-driven Simulation Loop.
- [x] Define solution structure across 4 projects: `ATCsim.UI`, `ATCsim.Core`, `ATCsim.Data`, `ATCsim.Tests`.
- [x] Construct component responsibility matrix and Mermaid component interaction diagram.
- [x] Design normalized 9-entity relational model ([docs/db_diagram.png](docs/db_diagram.png)):
  - `airports`, `runways`, `aircraft_models`, `aircrafts`, `flights`, `communication_logs`, `weather`, `weather_zones`, `sim_logs`.
- [x] Document engineering rationales and finalize [docs/Report_04.md](docs/Report_04.md).

---

### Stage 5: Solution Decomposition & MVP Baseline [IN PROGRESS]
*Objective: Build an end-to-end working baseline where UI, Core, and Data layers communicate.*

- [x] Setup initial `ATCsim.sln` solution and WPF desktop project on .NET 10.
- [x] Construct minimalist radar UI layout with Montserrat typography, status cards, and SVG icons.
- [ ] Separate solution into dedicated class library assemblies:
  - [ ] `ATCsim.Core` (.NET 10 class library) — domain entities, kinematics, world generator interface.
  - [ ] `ATCsim.Data` (.NET 10 class library) — SQLite database context and repositories.
  - [ ] `ATCsim.Tests` (xUnit test project).
- [ ] Configure local SQLite persistence:
  - [ ] Implement ADO.NET / lightweight ORM data layer.
  - [ ] Automated schema initialization on initial startup based on the 9-entity ER diagram.
- [ ] Minimal End-to-End Walkthrough:
  - [ ] Load seed and airport list from SQLite.
  - [ ] Initialize single aircraft track and render on Canvas via ViewModel data binding.
- [ ] Verify zero build warnings and zero errors.

---

### Stage 6: Iterative Feature Implementation [PLANNED]
*Objective: Implement core simulator logic using isolated feature branches and PRs.*

- [ ] Procedural Sector Generator (`ATCsim.Core.WorldGeneration`, Owner: M. Borkov):
  - [ ] Deterministic procedural sector generation algorithm driven by `world_seed`.
  - [ ] Positioning of airports, runway headings, and navigation waypoints.
  - [ ] Generation of dynamic weather zones (storm fronts, turbulence).
- [ ] Kinematics Simulation Engine (`ATCsim.Core.Kinematics`, Owner: O. Bezkorovainyi):
  - [ ] Fixed-timestep simulation loop (`SimulationLoop`) with speed multipliers (1x, 2x, 4x, pause).
  - [ ] Aircraft spatial coordinate integration based on groundspeed, magnetic heading, altitude, and wind vectors.
  - [ ] Standard turn rate calculations and vertical climb/descent profiles.
- [ ] Separation & Conflict Detector (`ATCsim.Core.Safety`, Owner: O. Bezkorovainyi):
  - [ ] Real-time pairwise distance calculation (horizontal < 5 NM, vertical < 1000 ft).
  - [ ] Alert generation and visual radar canvas pulse for conflicting tracks.
- [ ] Radio Exchange Subsystem (`ATCsim.Core.Radio`, Owner: M. Borkov):
  - [ ] Controller dispatch command pipeline (heading, altitude, speed, runway assignments).
  - [ ] Synthetic pilot readback generator conforming to ICAO phraseology.
  - [ ] Real-time append to `communication_logs`.
- [ ] UI Interaction & Canvas Rendering (`ATCsim.UI`, Owner: O. Bezkorovainyi, M. Borkov):
  - [ ] Track inspection: selection of active aircraft, command inputs, flight strip updates.
  - [ ] Dynamic path rendering: altitude gradient historic trail, dashed forward projection vector.

---

### Stage 7: Automated Testing & Quality Assurance [PLANNED]
*Objective: Build unit, integration, and edge-case test suites.*

- [ ] Test Strategy Definition (Owner: V. Ruban):
  - [ ] Define test boundaries, test matrix, and coverage targets.
- [ ] Unit Tests (`ATCsim.Tests`):
  - [ ] World generation determinism: identical `world_seed` guarantees identical coordinates.
  - [ ] Kinematics accuracy: coordinate delta matches groundspeed and wind drift vectors.
  - [ ] Conflict detection boundaries: verify trigger on 4.9 NM vs no trigger on 5.1 NM.
  - [ ] Input validation: reject out-of-range headings (<0 or >=360) and invalid altitudes.
- [ ] Integration Tests:
  - [ ] SQLite database read/write cycles, foreign key cascade behaviors.
  - [ ] Session state save and restore roundtrips.
- [ ] Performance Profiling:
  - [ ] Benchmark WPF Canvas rendering with 50+ simultaneous moving tracks (target >= 60 FPS).

---

### Stage 8: Continuous Integration (CI/CD) [PLANNED]
*Objective: Automate build and test validation via GitHub Actions.*

- [ ] Create workflow file `.github/workflows/ci.yml` (Owner: V. Ruban).
- [ ] Automated build step: `dotnet build --configuration Release`.
- [ ] Automated test step: `dotnet test --no-build --verbosity normal`.
- [ ] Enforce PR status checks to block merging on failure.

---

### Stage 9: Logging, Exception Handling & Security Audit [PLANNED]
*Objective: Guarantee application stability, auditability, and data security.*

- [ ] Structured Logging (Owner: M. Borkov):
  - [ ] Integrate structured logging (Serilog or `Microsoft.Extensions.Logging`).
  - [ ] Log lifecycle events: session start/stop, separation loss alerts, database failures.
  - [ ] Log file rotation in local application data directory.
- [ ] Global Exception Handling (Owner: O. Bezkorovainyi):
  - [ ] Register `DispatcherUnhandledException` in WPF `App.xaml.cs`.
  - [ ] Eliminate empty `catch { }` blocks across the codebase.
- [ ] Security Audit (Owner: V. Ruban):
  - [ ] Sanitize file paths against Path Traversal vulnerabilities.
  - [ ] Verify zero hardcoded secrets or sensitive credentials.

---

### Stage 10: Release Finalization & Project Defense [PLANNED]
*Objective: Deliver stable release, complete user docs, and verify defense readiness.*

- [ ] Defect resolution and bug scrub.
- [ ] End-to-end regression validation.
- [ ] Update root `README.md` with build and execution instructions.
- [ ] Prepare demonstration scenarios and technical presentation.
- [ ] Verify each team member can defend their code and commits.

---

## 4. Operational Instructions for LLMs

1. Always check the current active stage before adding or modifying code.
2. Maintain strict separation of layers: do not add UI dependencies to `Core` or `Data`.
3. Do not modify or remove fields from the 9-entity database schema without updating reports.
4. Verify compilation with `dotnet build` (0 Errors, 0 Warnings) before completing any task.
5. Update the corresponding checkbox `[ ]` -> `[x]` upon verifying changes.
