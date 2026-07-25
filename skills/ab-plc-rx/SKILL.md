---
name: ab-plc-rx
description: Implement, review, or troubleshoot reactive Allen-Bradley PLC access with ABPlcRx. Use for libplctag tag registration, scanning, reads, writes, observables, simulator tests, and generated PLC stream models.
---

# ABPlcRx

Use `IoT.DriverCore.ABPlcRx` for the default package surface. Register each physical tag with an explicit logical variable, group, and generic type witness before reading, writing, or subscribing.

Treat writes as safety-critical. Validate addressing, route, data type, bit index, and operational interlocks against a non-production controller. Use `short` plus a 0–15 bit index for SLC/PLC-5 word bits; do not apply word-bit access to a native boolean tag.

Manage client and subscription lifetimes explicitly. Use `AutoWriteValue = false` with `Write` for deliberate staged writes; inspect `PlcTagResult.StatusCode`, `PlcTagStatus.DecodeError`, and `ObserveErrors` rather than ignoring failures. Prefer `ReadManyAsync`, `WriteManyAsync`, or grouped scans for multi-tag work.

Use `ABPlcSimulator` to test registrations, values, reconnect behavior, and injected faults without hardware. For constructor, API, and source-generator details, read the co-packaged `../../README.md`; in a repository checkout use `packagereadme/ABPlcRx/README.md`.

`ABPlcRx` and `ABPlcRx.Reactive` already embed the matching source generator. Install `ABPlcRx.Generators` only when the analyzer must be pinned or managed independently, and never load a second generator version alongside the embedded analyzer.
