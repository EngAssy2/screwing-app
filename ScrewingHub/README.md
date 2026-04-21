# ScrewingHub 23

ScrewingHub is a comprehensive screw fastening monitoring and data logging system designed to integrate with **Omron CJ2M PLCs** and **DTM10 Monitors**. It provides real-time judgment (OK/NG), automated reset capabilities, and detailed CSV/Batch logging for production traceability.

## 🏗 System Architecture

The project is divided into three main layers:
- **ScrewingHub.Core**: Contains the domain models, shared interfaces, and core services like CSV logging, standardized unit conversion, and dual-layer statistics tracking.
- **ScrewingHub.Communication**: Implements the communication protocols:
    - **DTM10**: Serial communication (38400 baud) with DTM10 monitors to receive raw current and torque values.
    - **Omron PLC**: Optimized Host Link (C-mode) protocol for industrial signaling.
- **ScrewingHub.App**: The WPF application providing the user interface for monitoring, detailed history, and system configuration.

## ⚖️ Standardized Units
To ensure consistency across the plant, all torque values are standardized to **kgf·cm**.
- UI Displays (Dashboard, History, Settings) use `kgf·cm`.
- Log files (CSV, Batch) explicitly label torque columns as `(kgf·cm)`.
- Conversion factors are applied at the core level to transform raw sensor values into the standard unit.

## 📊 Production Monitoring & Stats
The application tracks performance at two granular levels:
- **Unit Statistics**: Tracks completed physical products. A unit is "Accepted" only if all screws are OK. An "NG" on any screw immediately marks the unit as "Rejected".
- **Screw Point Statistics**: Tracks individual screw performance (Judgment, Torque, and Time) for detailed process analysis.

## 📟 PLC Communication (Omron Host Link)

The application communicates with the PLC via RS232 using an optimized Host Link (C-mode) protocol tailored for high reliability even at low baud rates.

### Serial Configuration
- **Baud Rate**: Fully configurable (Standard: 9600 up to **115,200 bps** for CJ2M).
- **Data Bits**: 7 | **Parity**: Even | **Stop Bits**: 2 | **Handshake**: None.

### Optimized Communication Tuning
To prevent PLC alarms on busy serial links, the app features:
- **Stabilized Heartbeat**: Uses a single "Force Write 1" transaction to reduce serial overhead by 50% vs traditional handshakes.
- **Configurable Polling**: Independent intervals for Heartbeat (e.g. 200ms) and Reset Request Polling (e.g. 500ms) to prioritize production-critical writes.

### Memory Mapping
| Memory Address | Function | Value |
| :--- | :--- | :--- |
| **DM 1010** | **Pass (OK)** | 1 = Current Screw OK |
| **DM 1012** | **Fail (NG)** | 1 = Current Screw NG |
| **DM 1014** | **Error** | 1 = System Error |
| **DM 1016** | **Alive (Heartbeat)** | Writes 1 at configured interval |
| **DM 1000** | **Loose (Reset)** | 1 = PLC Request Reset, 0 = Acknowledged by App |

## 📊 Logging System
ScrewingHub generates robust logs stored in the configured `CSV Log Folder`:
1. **CSV Daily Logs**: Categorized into `/Raw`, `/Accept`, and `/Reject` subfolders.
2. **Batch Unit Reports**: Grouped reports containing all screws for a single unit with model-specific metadata.
3. **Naming placeholders**: supports `[Status]`, `[ModelNumber]`, `[Date]`, and `[StationName]`.

---
*Last Updated: 2026-04-21*
