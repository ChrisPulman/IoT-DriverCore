---
name: twincat-rx
description: Implement, review, or troubleshoot reactive TwinCAT ADS access with CP.TwinCATRx. Use for ADS routes, notification settings, correlated reads/writes, typed observables, structured tags, and in-memory ADS tests.
---

# TwinCATRx

Use `IoT.DriverCore.TwinCATRx` and `IoT.DriverCore.TwinCATRx.Core`. Configure the ADS address, port, and settings id; register notifications and writable variables before `Connect`; wait for `InitializeComplete` before operational I/O.

`CP.TwinCATRx` and `CP.TwinCATRx.Reactive` already embed the matching source generator. Install `TwinCATRx.Generators` only when the analyzer must be pinned or managed independently, and do not load a duplicate generator version.

Treat PLC writes as safety-critical. Validate every variable name, value type, string/array length, route, and controller-side interlock. Observe `ErrorReceived` and `OnWrite`; use correlation ids when concurrent one-shot reads or writes need distinct results.

Use typed `Observe`/`ObserveAsyncObservable` with explicit conversion. Use `CreateStruct` and `WriteValues` only after checking trimming/AOT implications; register required members when publishing trimmed applications. Use `InMemoryAdsClient` to test symbols, faults, reconnection, and metrics without an ADS runtime.

For generated clients, choose `[TwinCatReactiveStream]` or `[TwinCatPlcConnection]` deliberately, declare direct, structured, and write-only members with the documented attributes, build once to inspect diagnostics and generated members, then verify reads, writes, observations, and disposal against `InMemoryAdsClient`.

For the complete public API, generator attribute matrix, configuration overloads, and service-monitoring constraints, read the co-packaged `../../README.md`; in a repository checkout use `packagereadme/TwinCATRx/README.md`.
