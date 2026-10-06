# S7CommPlus symbolic and secure communication

## Reference analysis

The comparison uses [S7CommPlusDriver at dbd61e447c7aaf4486cf1f1fe0201212a6bd93c8](https://github.com/thomas-v2/S7CommPlusDriver/tree/dbd61e447c7aaf4486cf1f1fe0201212a6bd93c8). Its source was inspected outside this repository to identify protocol behavior. Its implementation, native binaries, compression dictionaries, and sample PLC data are not dependencies or bundled assets.

The important distinction is between offline aliases for classic S7 memory addresses and controller-resolved symbolic identifiers. Optimized S7-1200/1500 blocks need the latter. TLS alone does not make the classic S7 protocol understand optimized symbolic blocks: S7CommPlus has its own session establishment, object discovery, typed values, and requests.

## Implementation map

| Reference capability | Independent S7PlcRx implementation |
| --- | --- |
| ISO-on-TCP with the HMI endpoint | COTP negotiation and checked TPKT framing in `S7PlusTransport` |
| Encrypted S7CommPlus communication | TLS 1.3 over COTP using the maintained managed BouncyCastle.Cryptography dependency |
| Initial session creation and version negotiation | `S7PlusSession` creates session objects and acknowledges the controller's version structure |
| Password challenge authentication | An isolated protocol authentication codec produces the controller-mandated challenge response |
| Newer user/password authentication | TLS exporter, controller challenge, and encrypted authentication structure; automatic firmware selection or explicit mode |
| Online block discovery and type browsing | Lazy exploration of program blocks and native memory-area type information |
| Optimized block access by symbol | `S7SymbolicClient.ResolveAsync`, `ReadAsync`, and `WriteAsync` use local-identifier chains rather than byte offsets |
| Nested structures and array elements | Quoted path parsing, metadata type relations, declared lower bounds, multidimensional indexing, and array-of-structure navigation |
| Multi-value reads and writes | Native requests bounded by PLC-advertised resource limits, ordered results, and individual access errors |
| PLC primitive and extended values | Checked wire values plus CLR conversions for signed/unsigned integers, floating-point values, strings, date/time values, and DTL nanoseconds |
| Native change subscriptions | PLC subscription objects, item-reference mapping, credit renewal, bounded queues, cancellation, and deletion |
| Alarm subscriptions and metadata | Subscription objects, notification data, localized text structures, and associated values retained as typed metadata |
| RUN/STOP requests | Explicit execution-unit operating-state requests; reading the requested state is not presented as measuring the actual CPU state |
| Comment metadata | Raw blobs and bounded XML decompression; caller-supplied preset compression dictionaries where required |
| Existing common tag APIs | `S7SymbolicLogicalTagClient` composes the native client with `LogicalTagCatalog` and `ILogicalTagClient` |
| System.Reactive variant | The same sources compile under `REACTIVE_SHIM` into the Reactive package |

The implementation uses a bounded reader/writer and shared typed-value representation instead of the reference's hierarchy of individual value classes. TLS and protocol state have separate ownership. Discovery is cached per connection and invalidated when a fresh session is established.

## Certificate trust and authentication

The default policy checks the peer certificate's validity, host identity, system trust chain, and server-authentication purpose. An exact SHA-256 certificate pin supports self-signed PLC certificates without an accept-all default. A certificate validation callback is an explicit application policy. Authentication and certificate failures do not trigger a downgrade to classic or cleartext S7.

TLS 1.3 and its exporter are supplied by BouncyCastle.Cryptography, including on the older framework targets. The controller's legacy challenge uses SHA-1 as a wire-defined transformation; it is not an application password-storage scheme. New authentication uses the TLS exporter and the controller-supplied initialization vector. Substituting unrelated password hashing or a random initialization vector would produce incompatible messages. These protocol transformations are isolated and tested independently.

The client does not export TLS session secrets to diagnostic log files. Credentials should come from an application secret provider and should not be written into a repository or diagnostic output.

## Symbol paths and values

Examples of native paths are `"Drive".ActualSpeed`, `"Drive".State.Ready`, and `"Drive".Samples[-2]`. Quoting distinguishes a PLC identifier containing punctuation from a path separator. Array indexing honors the lower bounds advertised by the controller rather than assuming zero-based PLC arrays.

`S7Symbol` retains accessibility, read-only status, optimization information, checksums, string limits, and array bounds. The wire address carries the native area/subarea and local identifiers. A declaration's checksum metadata is distinct from the optional checksum field in a wire access address.

Native input, output, marker, counter, and timer areas use decimal relation identifiers 80 through 84 and actual-value subarea 3736. Empty native counter/timer type descriptions are valid browse results.

`ReadAsync(new LogicalTagKey<float>(path), cancellationToken)` converts a discovered PLC value to a typed CLR result. The non-generic overload retains the wire type and flags. Individual string or date/time array elements can be addressed independently when the protocol represents each element using an embedded array. Multiple element writes are not an atomic PLC transaction.

Packed structures retain their type identifier, interface timestamp, transport flags, and bytes. DTL conversion retains nanoseconds that cannot fit in ordinary `DateTime` precision. Explicit packed-structure writes must use the controller's matching type/interface metadata.

## Compression dictionaries

No preset dictionary data from the reference is included. PLC comment and text blobs can require a dictionary identified by its Adler-32 checksum. Supply the dictionary bytes through the metadata decompression API when the controller uses that format. The decoder validates the supplied dictionary, limits decompressed output, checks the stream checksum, and prohibits XML DTD processing. Missing dictionaries produce an explicit error while the original blob remains available.

## Boundaries and verification

This client targets the TLS-capable S7CommPlus HMI endpoint. Firmware and the TIA project configuration must support that endpoint. The inspected reference associates it with S7-1500 firmware 2.9 or later and S7-1200 TLS-capable configurations; TLS 1.3 availability on S7-1200 is associated with firmware 4.5 or later. The client does not make older firmware understand this protocol.

The reference does not provide working Variant addressing or implemented generic S7String wire payloads. Those unverified wire forms are explicitly rejected; ordinary PLC STRING/WSTRING access uses their verified array representations. Reference GUI tools, sample projects, secret logging, and native library packaging are not driver capabilities.

Automated verification includes independently expected wire vectors, malformed-message checks, metadata/indexing boundaries, certificate-policy rejection, authentication vectors, subscription lifecycle tests, and a genuine TLS-over-COTP loopback peer. The loopback peer is independently implemented and checks session, authentication, browse, and data operations. These tests establish protocol and implementation behavior in the tested fixtures; they do not replace interoperability testing against each intended physical PLC and firmware.

Final verification on 2026-10-06: `dotnet build src/IoT-DriverCore.slnx` completed with zero warnings and zero errors across all solution target frameworks. The complete Microsoft.Testing.Platform/TUnit solution run passed 10,799 tests with zero failures and zero skips. The focused symbolic set passed 90 tests. Coverage was collected per module across the full run and inspected through Mtpunittestmcp. No diagnostic suppressions were introduced.

Physical validation on 2026-10-06 used the authorized S7-1500 at 172.16.13.1. Read-only diagnostics identified PLC_1, CPU 1516-3 PN/DP, order number 6ES7 516-3AP03-0AB0, firmware 4.0.0. With the explicitly authorized certificate pin, TLS connection and online browse returned 731 symbols. Three logger scalar reads, indexed STRING and REAL reads, a BOOL read, a native BOOL subscription notification, subscription deletion, and graceful disconnect succeeded. Both opt-in TUnit hardware checks passed. A different exported certificate pin was rejected. No user-data writes or CPU operating-state changes were performed; writes and operating-state requests were validated against controlled peers.

The opt-in `PlusLiveHardwareTests` fixture is compiled with `EnableLiveS7Tests=true`. It requires `S7PLCRX_LIVE_CERTIFICATE_SHA256`, uses `S7PLCRX_LIVE_IP` for the endpoint, and optionally accepts credentials through `S7PLCRX_LIVE_USERNAME` and `S7PLCRX_LIVE_PASSWORD`. It browses and reads a bounded number of accessible logger scalars. Run it explicitly by class filter; ordinary test runs do not contact this PLC.
