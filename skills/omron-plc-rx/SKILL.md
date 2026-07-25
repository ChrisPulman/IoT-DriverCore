---
name: omron-plc-rx
description: Use when implementing, reviewing, or troubleshooting Omron FINS TCP, UDP, Host Link FINS, or Toolbus serial integrations with OmronPlcRx or OmronPlcRx.Reactive.
---

# OmronPlcRx

Use `IoT.DriverCore.OmronPlcRx` for the base package; use the `.Reactive` namespace only with `OmronPlcRx.Reactive`.

The runtime packages already embed the matching source generator. Install `OmronPlcRx.Generators` only when the analyzer must be pinned or managed independently, and do not load a duplicate generator version.

Create `OmronConnectionOptions`, construct `OmronPlcRx(options, pollInterval)`, register `PlcTag<T>`, and use the same-name, same-type `LogicalTagKey<T>` for `Observe`, `ReadValueAsync`, `WriteValueAsync`, `GetValue`, and `SetValue`.

Subscribe to `Errors`. Prefer `WriteValueAsync` for commands that require an observable result; regard `SetValue` as queued background work. Validate serial settings with `OmronSerialOptions.Validate()` and use `CreateToolbus` only for Toolbus endpoints.

Treat PLC writes as safety-sensitive. Verify node IDs, transport, endpoint, address, tag type, and PLC permissions before enabling writes or reducing poll intervals.

For the complete public API, address/type guidance, examples, and troubleshooting, read the co-packaged `../../README.md`; in a repository checkout use `packagereadme/OmronPlcRx/README.md`.
