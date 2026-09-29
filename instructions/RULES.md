# Engineering Rules and System Instructions (RULES.md)

This file contains strict operational rules and architectural constraints for any LLM/AI assistant working on the ATCsim codebase.

---

## 1. Project Overview

- **Name:** ATCsim (Air Traffic Control Simulator)
- **Target Platform:** .NET 10 (or .NET 8 LTS), C# 12+
- **Application Type:** Desktop application using WPF and XAML
- **Data Storage:** SQLite via ADO.NET / lightweight ORM
- **Core Capabilities:**
  - Deterministic procedural sector generation based on an integer `world_seed`.
  - Kinematics simulation loop (velocity, heading, altitude, wind drift).
  - Loss-of-separation and collision alert detection.
  - Pilot-controller radio exchange journal adhering to ICAO phraseology.

---

## 2. Architecture & Layering Rules

The solution `ATCsim.sln` is structured into 4 isolated projects:
1. `ATCsim.UI`: WPF presentation layer (Views, ViewModels, Converters, Resources).
2. `ATCsim.Core`: Pure domain logic (Entities, Kinematics, WorldGenerator, CollisionDetection, Weather). Must have ZERO dependencies on UI or Data layers.
3. `ATCsim.Data`: Persistence layer (SQLite context, repositories, session export/import).
4. `ATCsim.Tests`: Unit and integration test suite (xUnit).

### Strict Architectural Invariants:
- **No Code-Behind Business Logic:** `*.xaml.cs` must NEVER contain physics calculations, flight tracking, collision math, or database queries. All UI behavior must flow through ViewModels, data binding, and `ICommand`.
- **Deterministic World Generation:** `WorldGenerator` must produce 100% identical sector geometry for a given `world_seed`. Never call unseeded `new Random()`. Always use seed-initialized pseudo-random generators.
- **Thread Safety:** The simulation loop must execute off the UI thread. State synchronization to ViewModels must use event subscription or dispatcher calls.

---

## 3. UI/UX Design System (Swiss Minimalist)

All XAML code must strictly follow these design parameters:

- **Color Palette:**
  - Window background: Pure white `#FFFFFF` or clean light gray `#F8F9FA`.
  - Panels and cards: `#FFFFFF` or `#F1F3F5`.
  - Borders: Thin `1px` solid `#E9ECEF` or `#CED4DA`.
  - Text: High-contrast `#212529`, secondary labels `#6C757D`.
  - Accent (Normal / Active / On Time): Calm green `#2B8A3E` (badge background `#EBFBEE`).
  - Warning / Alert / Conflict: Subdued red `#C92A2A`.
  - Altitude Trajectory Gradient: Low `#FCC419` -> Medium `#F76707` -> Cruise `#7048E8`.
- **Strictly Prohibited UI Styles:**
  - Dark mode, neon glows, cyberpunk aesthetic, blurred colored background orbs, heavy drop shadows.
- **Geometry:**
  - Sharp rectangular corners. Set `CornerRadius="0"` or maximum `CornerRadius="2"`. Never use pill-shaped or bubbly cards.
- **Icons:**
  - Monochrome black vector SVG paths only (`<Path Data="..." Fill="#212529" />`). Never use emojis or raster bitmap icons.
- **Typography:**
  - Font family: `Montserrat`, fallback to `Segoe UI, sans-serif`.
  - Use tabular/monospaced digits for coordinates, headings, and altitudes to prevent layout jitter.

---

## 4. Data Model Specifications

The domain model must strictly mirror the 9 entities documented in `docs/Report_task_3.md` (and `docs/Report_04.md` / `docs/db_diagram.png`):
- `airports`: id, icao (4 uppercase chars, unique), name, coordinates (x, y), max_capacity.
- `runways`: id, airport_id, designator, heading_angle (0-359), length_meters, is_available.
- `aircraft_models`: id, model_name, category, cruise_speed, max_altitude, fuel_capacity, icon_type.
- `aircrafts`: id, callsign, model_id, is_active, coordinates (x, y), altitude, fuel_remaining.
- `flights`: id, flight_number, aircraft_id, departure_airport_id, arrival_airport_id, eta, delay_minutes, is_delayed.
- `communication_logs`: id, airport_id, aircraft_id, sender_type, message, timestamp.
- `weather`: id, grid_coordinates, wind_speed, wind_direction.
- `weather_zones`: id, zone_type (STORM, TURBULENCE, RESTRICTED), center_coordinates, radius, altitude_min, altitude_max.
- `sim_logs`: id, world_seed, event_type, details, timestamp.

Do not delete or rename these fields in C# entities without corresponding documentation updates.

---

## 5. Code Quality & Robustness

- **C# Standards:** Use C# 12+, file-scoped namespaces, and `#nullable enable`.
- **Exception Handling:**
  - Zero unhandled crash policy.
  - Never use empty `catch { }` blocks. Always log or handle exceptions.
- **Input Validation:**
  - Validate headings (0 to 359 degrees), altitudes (>= 0 ft), coordinates, and seeds before processing in simulation loops.
- **Security:**
  - Do not hardcode secrets, connection credentials, or tokens.
  - Sanitize file paths when saving or loading worlds to prevent Path Traversal.

---

## 6. Git Workflow & Task Tracking

- **Repository:** `https://github.com/dyxell1n/ATCsim`
- **Branching Policy:**
  - Never push directly to `main`.
  - Branches: `feature/<task>`, `bugfix/<issue>`, `docs/<report>`, `test/<suite>`.
- **Commit Format:** Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`).
- **Task Tracking:** Always check `PLAN.md` before starting work, and mark completed checkboxes (`[ ]` -> `[x]`).
- **Build Verification:** Always execute `dotnet build` before finishing a turn. Code must compile with 0 Errors and 0 Warnings.
