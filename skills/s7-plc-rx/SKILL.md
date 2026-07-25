---
name: s7-plc-rx
description: Use when implementing, reviewing, or troubleshooting Siemens S7 integrations with S7PlcRx or S7PlcRx.Reactive, including typed tags, polling, bindings, batch work, and diagnostics.
---

# S7PlcRx

Use `IoT.DriverCore.S7PlcRx` for the base package; use the `.Reactive` namespace only with `S7PlcRx.Reactive`.

Install `S7PlcRx.Generators` alongside the selected runtime package when generated bindings are required; the runtime packages do not embed this analyzer.

Create an `IRxS7` through the CPU factory or `RxS7Options` and dispose it: `using IRxS7 plc = S71500.Create(...)`. `IRxS7` inherits disposable `ReactiveUI.Primitives.Disposables.ICancelable`. Register tags with `TagOperations.AddUpdateTagItem(plc, typeof(T), name, address[, length])`, then use a matching `LogicalTagKey<T>` with `Observe<T>` and `ReadAsync<T>`; use `Value<T>` only after the tag and type have been verified.

Subscribe to `IsConnected`, `LastError`, and `LastErrorCode`. Configure watchdogs only with a reviewed `DBW` address and safe interval. Confirm CPU family, rack/slot, DB/bit address syntax, PLC protection, and machine interlocks before writing.

Use generator attributes only on partial binding types. Use batch, logical-tag, optimisation, and production APIs after establishing a baseline with the basic tag flow.

For the complete public API, supported addressing, examples, operational guidance, and troubleshooting, read the co-packaged `../../README.md`; in a repository checkout use `packagereadme/S7PlcRx/README.md`.
