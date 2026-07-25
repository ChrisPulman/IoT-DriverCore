---
name: mitsubishi-rx
description: Implement, review, or document Mitsubishi MC Protocol and SLMP integrations using MitsubishiRx or MitsubishiRx.Reactive. Use when configuring Mitsubishi PLC transports, direct devices, tags, polling, write pipelines, serial frames, or simulator-backed tests.
---

# MitsubishiRx

Before changing an integration, read the co-packaged `../../README.md`; in a repository checkout use `packagereadme/MitsubishiRx/README.md`.

- Import `IoT.DriverCore.MitsubishiRx` or the `.Reactive` namespace that matches the selected package; never mix both surfaces casually.
- Both runtime packages carry the analyzer, but the current generator emits the base `IoT.DriverCore.MitsubishiRx` surface and requires the base `MitsubishiRx` runtime. Install `MitsubishiRx.Generators` only alongside that base runtime when the analyzer must be versioned independently; use handwritten APIs in a `.Reactive`-only project and never load duplicate analyzer versions.
- Construct `MitsubishiRx` with `MitsubishiClientOptions`, an optional `IMitsubishiTransport`, and an optional scheduler. Check `Responce.IsSucceed` before reading `Value`.
- Match frame, data code, transport, route, serial format, and X/Y notation to the real PLC/module configuration. Require `MitsubishiSerialOptions` for serial transport.
- Use cancellation tokens, dispose clients/subscriptions, batch contiguous reads, and serialize or coalesce writes.
- Treat remote control, passwords, memory, raw commands, and writes as safety-sensitive; require explicit interlocks and audit evidence.
- Validate tag databases and preview diffs before rollout. Use `MitsubishiSimulatorTransport` / `MitsubishiSimulatorMemory` for deterministic tests.
- For generated clients, define and validate the supported Mitsubishi attributes, build once to inspect the generated surface, then exercise generated reads, writes, and observations against a simulator before connecting to a PLC.
- Inspect `src/MitsubishiRx` before describing an API; shared reactive source changes namespaces under `REACTIVE_SHIM`.
