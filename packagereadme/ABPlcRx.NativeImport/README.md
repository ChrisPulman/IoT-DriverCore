# IoT-Driver.ABPlcRx.NativeImport

`IoT-Driver.ABPlcRx.NativeImport` is the stable IoT-Driver redistribution of the
exact [`libplctag.NativeImport` 2.0.0-alpha.8](https://www.nuget.org/packages/libplctag.NativeImport/2.0.0-alpha.8)
asset distribution used by the AB PLC drivers.

It contains the upstream managed assemblies for all upstream target frameworks,
the nine upstream native runtime assets, and the upstream `build` and
`buildTransitive` MSBuild assets. Byte-identical aliases of the MSBuild files use
the stable package ID so NuGet imports them for consumers. It is intended as an
implementation dependency of `IoT-Driver.ABPlcRx` packages, rather than as an
application-facing API.

## Provenance and license

The redistributed upstream assets are unmodified and originate from
[`libplctag/libplctag.NET` commit `ee026b8911508006e6cb493e231a6aa07d4088dc`](https://github.com/libplctag/libplctag.NET/tree/ee026b8911508006e6cb493e231a6aa07d4088dc).
They are licensed under the [Mozilla Public License 2.0](https://www.mozilla.org/MPL/2.0/).
The package contains a third-party notice and MPL-2.0 license reference under
`third-party/libplctag.NativeImport/`.

The package does not modify, replace, or add an API to the upstream distribution.
