# RAM → Graphics Pool Manager

A Windows .NET 8 utility that commits a configurable RAM-backed resource pool and monitors system/GPU memory.

## Important distinction

This software does **not** turn system RAM into physical dedicated VRAM. Dedicated GPU VRAM remains hardware attached to the GPU and is normally much faster. The application instead provides a controllable RAM-backed secondary memory tier that can be used by future integrations for graphics-resource caching, staging, and other workloads.

## Current features

- Windows desktop UI
- Configurable pool target from 0–16 GB
- Native Windows `VirtualAlloc` backing for committed RAM
- Live available-RAM monitoring
- GPU name and Windows-reported shared-memory telemetry
- Optional automatic pressure management with a minimum free-RAM reserve
- Safe cleanup when the application closes

## Build

Requirements:

- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 or `dotnet` CLI

```powershell
dotnet build .\RamVramManager.sln -c Release
```

Run:

```powershell
dotnet run --project .\src\RamVramManager\RamVramManager.csproj -c Release
```

## Roadmap

1. Direct3D 12 resource staging/cache integration.
2. GPU memory budget telemetry through DXGI/D3D12.
3. Per-application pools and priorities.
4. Texture/asset cache APIs for supported applications.
5. Startup/task-tray mode and profiles.
6. Installer and signed Windows release.

## Safety

The pool consumes real physical/committed system memory. Do not allocate more RAM than the machine can comfortably spare. Automatic mode keeps a configurable amount of RAM available and should be tested before use on memory-constrained systems.
