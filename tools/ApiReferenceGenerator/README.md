# API reference generator

This tool generates a Markdown API reference from built runtime assemblies and their compiler-produced XML documentation. It uses reflection to enumerate `Assembly.GetExportedTypes()` and `DeclaredOnly | Public` members, so it excludes private and internal implementation types and inherited members.

Run it after building the desired target framework:

```powershell
dotnet run --project tools/ApiReferenceGenerator -- `
  --assembly src/ABPlcRx/bin/Release/net10.0/ABPlcRx.dll `
  --output artifacts/docs/ABPlcRx-api.md
```

The XML file is assumed to sit beside each DLL with the same base name. Pass `--xml <path>` immediately after an `--assembly` to override that association.

Multiple `--assembly` inputs create one combined document. This is intended for TwinCAT's runtime split; use assemblies built for the same target framework:

```powershell
dotnet run --project tools/ApiReferenceGenerator -- `
  --assembly src/TwinCATRx.Core/bin/Release/net10.0/TwinCATRx.Core.dll `
  --assembly src/TwinCATRx/bin/Release/net10.0/TwinCATRx.dll `
  --output artifacts/docs/TwinCATRx-api.md
```

Each entry includes its XML documentation ID, a C#-style reflection signature, summary, parameter descriptions, return description, and value description. XML comments are preferred; when a public declaration has no emitted comment, the tool supplies a conservative signature-derived description instead of leaving a documentation hole. Compiler-only record clone methods are omitted. Generated files are reports, not hand-authored package READMEs.

## Package README fragments

Use `--readme` instead of `--output` to maintain an exhaustive API catalogue inside a package README:

```powershell
dotnet run --project tools/ApiReferenceGenerator -- `
  --assembly src/ABPlcRx/bin/Release/net10.0/ABPlcRx.dll `
  --readme packagereadme/ABPlcRx/README.md
```

The modes are mutually exclusive. README mode appends a marked block when it is absent and replaces precisely that block on later runs:

```markdown
<!-- BEGIN GENERATED PUBLIC API -->
## Exhaustive public API reference
...
<!-- END GENERATED PUBLIC API -->
```

The fragment uses heading levels that nest below the package README (`##` catalogue, `###` assembly, `####` type, and `######` member) and intentionally omits local absolute assembly paths. Keep hand-authored explanation, safety guidance, and examples outside the markers; the generator owns only the marked block.

Limitations: reflection reports the public surface of the supplied build only, so select the intended target framework and ensure its dependent assemblies remain beside the DLLs. XML `<inheritdoc/>` is identified but is not resolved across assemblies. C#-style signatures reflect runtime metadata and can therefore omit source-only syntax such as nullable annotations, `required`, default parameter values, and custom attributes.
