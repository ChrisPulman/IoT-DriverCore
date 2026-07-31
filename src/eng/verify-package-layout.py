#!/usr/bin/env python3
"""Verify the repository's NuGet package identities and source-generator isolation."""

from __future__ import annotations

import argparse
import sys
import zipfile
from pathlib import Path, PurePosixPath
from xml.etree import ElementTree


EXPECTED_PACKAGES = {
    "IoT-Driver.Core",
    "IoT-Driver.ABPlcRx",
    "IoT-Driver.ABPlcRx.Reactive",
    "IoT-Driver.ABPlcRx.NativeImport",
    "IoT-Driver.ABPlcRx.Generators",
    "IoT-Driver.MitsubishiRx",
    "IoT-Driver.MitsubishiRx.Reactive",
    "IoT-Driver.MitsubishiRx.Generators",
    "IoT-Driver.ModbusRx",
    "IoT-Driver.ModbusRx.Reactive",
    "IoT-Driver.ModbusRx.Generators",
    "IoT-Driver.OmronPlcRx",
    "IoT-Driver.OmronPlcRx.Reactive",
    "IoT-Driver.OmronPlcRx.Generators",
    "IoT-Driver.S7PlcRx",
    "IoT-Driver.S7PlcRx.Reactive",
    "IoT-Driver.S7PlcRx.Generators",
    "IoT-Driver.SerialPortRx",
    "IoT-Driver.SerialPortRx.Reactive",
    "IoT-Driver.SerialPortRx.Generators",
    "IoT-Driver.TwinCATRx",
    "IoT-Driver.TwinCATRx.Reactive",
    "IoT-Driver.TwinCATRx.Core",
    "IoT-Driver.TwinCATRx.Core.Reactive",
    "IoT-Driver.TwinCATRx.Generators",
}

NATIVE_IMPORT_PACKAGE = "IoT-Driver.ABPlcRx.NativeImport"
UPSTREAM_NATIVE_IMPORT_PACKAGE = "libplctag.NativeImport"
UPSTREAM_NATIVE_IMPORT_VERSION = "2.0.0-alpha.8"
UPSTREAM_NATIVE_IMPORT_SHA256 = (
    "505110C23B82CE6E779AD198E3A345E8DD1721A6D260868DCE9E9AB6574EC8A2"
)
UPSTREAM_REPOSITORY_URL = "https://github.com/libplctag/libplctag.NET"
UPSTREAM_REPOSITORY_COMMIT = "ee026b8911508006e6cb493e231a6aa07d4088dc"

NATIVE_IMPORT_TFMS = {
    "net47",
    "net471",
    "net472",
    "net48",
    "net481",
    "net5.0",
    "net6.0",
    "net7.0",
    "net8.0",
    "netcoreapp3.0",
    "netcoreapp3.1",
}
NATIVE_IMPORT_DEPENDENCY_TFMS = {
    ".NETFramework4.7",
    ".NETFramework4.7.1",
    ".NETFramework4.7.2",
    ".NETFramework4.8",
    ".NETFramework4.8.1",
    ".NETCoreApp3.0",
    ".NETCoreApp3.1",
    "net5.0",
    "net6.0",
    "net7.0",
    "net8.0",
}
AB_RUNTIME_DEPENDENCY_TFMS = {
    ".NETFramework4.7.2",
    ".NETFramework4.8",
    ".NETFramework4.8.1",
    "net8.0",
    "net9.0",
    "net10.0",
    "net11.0",
}
NATIVE_IMPORT_RIDS = {
    "linux-arm": "libplctag.so",
    "linux-arm64": "libplctag.so",
    "linux-x64": "libplctag.so",
    "linux-x86": "libplctag.so",
    "osx-arm64": "libplctag.dylib",
    "osx-x64": "libplctag.dylib",
    "win-arm64": "plctag.dll",
    "win-x64": "plctag.dll",
    "win-x86": "plctag.dll",
}
NATIVE_IMPORT_BUILD_ASSETS = {
    "build/libplctag.NativeImport.props",
    "build/libplctag.NativeImport.targets",
    "build/IoT-Driver.ABPlcRx.NativeImport.props",
    "build/IoT-Driver.ABPlcRx.NativeImport.targets",
    "buildTransitive/libplctag.NativeImport.props",
    "buildTransitive/libplctag.NativeImport.targets",
    "buildTransitive/IoT-Driver.ABPlcRx.NativeImport.props",
    "buildTransitive/IoT-Driver.ABPlcRx.NativeImport.targets",
}

GENERATOR_ASSEMBLIES = {
    "IoT-Driver.ABPlcRx.Generators": "ABPlcRx.Generators.dll",
    "IoT-Driver.MitsubishiRx.Generators": "MitsubishiRx.Generators.dll",
    "IoT-Driver.ModbusRx.Generators": "ModbusRx.Generators.dll",
    "IoT-Driver.OmronPlcRx.Generators": "OmronPlcRx.Generators.dll",
    "IoT-Driver.S7PlcRx.Generators": "S7PlcRx.Generators.dll",
    "IoT-Driver.SerialPortRx.Generators": "SerialPortRx.Generators.dll",
    "IoT-Driver.TwinCATRx.Generators": "TwinCATRx.Generators.dll",
}


def read_package_id(package: Path, archive: zipfile.ZipFile) -> str:
    nuspec_names = [name for name in archive.namelist() if name.endswith(".nuspec")]
    if len(nuspec_names) != 1:
        raise ValueError(f"{package.name}: expected one .nuspec, found {len(nuspec_names)}")

    root = ElementTree.fromstring(archive.read(nuspec_names[0]))
    package_id = root.findtext(".//{*}metadata/{*}id")
    if not package_id:
        raise ValueError(f"{package.name}: package ID is missing from the .nuspec")

    return package_id


def verify_native_import_package(
    archive: zipfile.ZipFile, names: list[str], root: ElementTree.Element
) -> list[str]:
    """Verify the vendored alpha.8 package contract used by stable AB packages."""
    errors: list[str] = []
    metadata = root.find(".//{*}metadata")
    if metadata is None:
        return [f"{NATIVE_IMPORT_PACKAGE}: .nuspec metadata is missing"]

    version = metadata.findtext("{*}version") or ""
    if not version or "-" in version:
        errors.append(
            f"{NATIVE_IMPORT_PACKAGE}: wrapper version must be stable; found {version!r}"
        )

    license_element = metadata.find("{*}license")
    if (
        license_element is None
        or license_element.attrib.get("type") != "expression"
        or (license_element.text or "").strip() != "MPL-2.0"
    ):
        errors.append(
            f"{NATIVE_IMPORT_PACKAGE}: expected MPL-2.0 license expression metadata"
        )

    repository = metadata.find("{*}repository")
    if (
        repository is None
        or repository.attrib.get("type") != "git"
        or repository.attrib.get("url") != UPSTREAM_REPOSITORY_URL
        or repository.attrib.get("commit") != UPSTREAM_REPOSITORY_COMMIT
    ):
        errors.append(
            f"{NATIVE_IMPORT_PACKAGE}: repository provenance must identify "
            f"{UPSTREAM_REPOSITORY_URL}@{UPSTREAM_REPOSITORY_COMMIT}"
        )

    expected_managed_assets = {
        f"lib/{tfm}/libplctag.NativeImport.{extension}"
        for tfm in NATIVE_IMPORT_TFMS
        for extension in ("dll", "xml")
    }
    managed_assets = {name for name in names if name.lower().startswith("lib/")}
    if managed_assets != expected_managed_assets:
        errors.append(
            f"{NATIVE_IMPORT_PACKAGE}: managed assets must exactly reproduce alpha.8 "
            f"TFMs; missing {sorted(expected_managed_assets - managed_assets)!r}, "
            f"unexpected {sorted(managed_assets - expected_managed_assets)!r}"
        )

    expected_native_assets = {
        f"runtimes/{rid}/native/{file_name}"
        for rid, file_name in NATIVE_IMPORT_RIDS.items()
    }
    native_assets = {name for name in names if name.lower().startswith("runtimes/")}
    if native_assets != expected_native_assets:
        errors.append(
            f"{NATIVE_IMPORT_PACKAGE}: native assets must exactly reproduce alpha.8 "
            f"RIDs; missing {sorted(expected_native_assets - native_assets)!r}, "
            f"unexpected {sorted(native_assets - expected_native_assets)!r}"
        )

    build_assets = {
        name
        for name in names
        if name.lower().startswith(("build/", "buildtransitive/"))
    }
    if build_assets != NATIVE_IMPORT_BUILD_ASSETS:
        errors.append(
            f"{NATIVE_IMPORT_PACKAGE}: build and buildTransitive props/targets must "
            f"preserve alpha.8 assets and provide package-ID import aliases; missing "
            f"{sorted(NATIVE_IMPORT_BUILD_ASSETS - build_assets)!r}, unexpected "
            f"{sorted(build_assets - NATIVE_IMPORT_BUILD_ASSETS)!r}"
        )

    for directory in ("build", "buildTransitive"):
        for extension in ("props", "targets"):
            upstream_name = f"{directory}/libplctag.NativeImport.{extension}"
            alias_name = f"{directory}/{NATIVE_IMPORT_PACKAGE}.{extension}"
            if upstream_name in names and alias_name in names:
                if archive.read(upstream_name) != archive.read(alias_name):
                    errors.append(
                        f"{NATIVE_IMPORT_PACKAGE}: {alias_name} must be byte-for-byte "
                        f"identical to {upstream_name}"
                    )

    dependencies = metadata.findall(".//{*}dependency")
    dependency_ids = {dependency.attrib.get("id", "") for dependency in dependencies}
    if dependency_ids != {"System.Memory"}:
        errors.append(
            f"{NATIVE_IMPORT_PACKAGE}: expected only the upstream System.Memory "
            f"dependency; found {sorted(dependency_ids)!r}"
        )
    for dependency in dependencies:
        dependency_version = dependency.attrib.get("version", "")
        if "-" in dependency_version:
            errors.append(
                f"{NATIVE_IMPORT_PACKAGE}: stable wrapper has prerelease dependency "
                f"{dependency.attrib.get('id', '')} {dependency_version}"
            )

    dependency_groups = metadata.findall(".//{*}dependencies/{*}group")
    dependency_tfms = {
        group.attrib.get("targetFramework", "") for group in dependency_groups
    }
    if dependency_tfms != NATIVE_IMPORT_DEPENDENCY_TFMS:
        errors.append(
            f"{NATIVE_IMPORT_PACKAGE}: dependency groups must exactly reproduce "
            f"alpha.8; missing "
            f"{sorted(NATIVE_IMPORT_DEPENDENCY_TFMS - dependency_tfms)!r}, "
            f"unexpected {sorted(dependency_tfms - NATIVE_IMPORT_DEPENDENCY_TFMS)!r}"
        )
    for group in dependency_groups:
        group_dependencies = group.findall("{*}dependency")
        if len(group_dependencies) != 1 or any(
            dependency.attrib.get("id") != "System.Memory"
            or dependency.attrib.get("version") != "4.6.0"
            for dependency in group_dependencies
        ):
            errors.append(
                f"{NATIVE_IMPORT_PACKAGE}: dependency group "
                f"{group.attrib.get('targetFramework', '')} must contain only the "
                "upstream System.Memory 4.6.0 minimum dependency"
            )

    text_assets = {
        name: archive.read(name).decode("utf-8", errors="replace").lower()
        for name in names
        if name.lower().endswith((".md", ".txt"))
    }
    if not any(
        "mpl-2.0" in name.lower()
        or "mpl-2.0" in content
        or "mozilla public license" in content
        for name, content in text_assets.items()
    ):
        errors.append(
            f"{NATIVE_IMPORT_PACKAGE}: MPL-2.0 license/notice text is missing"
        )
    if not any(
        UPSTREAM_REPOSITORY_URL.lower() in content
        and UPSTREAM_NATIVE_IMPORT_VERSION in content
        and UPSTREAM_REPOSITORY_COMMIT in content
        and UPSTREAM_NATIVE_IMPORT_SHA256.lower() in content
        for content in text_assets.values()
    ):
        errors.append(
            f"{NATIVE_IMPORT_PACKAGE}: source notice must identify "
            f"{UPSTREAM_NATIVE_IMPORT_PACKAGE} {UPSTREAM_NATIVE_IMPORT_VERSION} and "
            f"{UPSTREAM_REPOSITORY_COMMIT}, including upstream nupkg SHA-256 "
            f"{UPSTREAM_NATIVE_IMPORT_SHA256}"
        )

    return errors


def verify_package(package: Path) -> tuple[str, list[str]]:
    errors: list[str] = []
    with zipfile.ZipFile(package) as archive:
        package_id = read_package_id(package, archive)
        names = [name.replace("\\", "/") for name in archive.namelist()]
        nuspec_name = next(name for name in archive.namelist() if name.endswith(".nuspec"))
        root = ElementTree.fromstring(archive.read(nuspec_name))
        package_version = root.findtext(".//{*}metadata/{*}version") or ""
        if package_version and "-" not in package_version:
            for dependency in root.findall(".//{*}dependency"):
                dependency_version = dependency.attrib.get("version", "")
                if "-" in dependency_version:
                    errors.append(
                        f"{package_id}: stable package has prerelease dependency "
                        f"{dependency.attrib.get('id', '')} {dependency_version}"
                    )
        generator_dlls = [
            name for name in names if PurePosixPath(name).name.endswith(".Generators.dll")
        ]
        analyzer_assets = [
            name for name in names if name.lower().startswith("analyzers/")
        ]

        if package_id in GENERATOR_ASSEMBLIES:
            expected_path = f"analyzers/dotnet/cs/{GENERATOR_ASSEMBLIES[package_id]}"
            if analyzer_assets != [expected_path]:
                errors.append(
                    f"{package_id}: expected only {expected_path!r} as an analyzer asset; "
                    f"found {analyzer_assets!r}"
                )
            if generator_dlls != [expected_path]:
                errors.append(
                    f"{package_id}: expected exactly one generator DLL at {expected_path!r}; "
                    f"found {generator_dlls!r}"
                )
            if any(name.lower().startswith("lib/") for name in names):
                errors.append(f"{package_id}: generator packages must not contain lib/ assets")
        else:
            if analyzer_assets:
                errors.append(
                    f"{package_id}: runtime package contains analyzer assets {analyzer_assets!r}"
                )
            if generator_dlls:
                errors.append(
                    f"{package_id}: runtime package contains generator DLLs {generator_dlls!r}"
                )

            dependency_ids = {
                element.attrib.get("id", "")
                for element in root.findall(".//{*}dependency")
            }
            generator_dependencies = dependency_ids & GENERATOR_ASSEMBLIES.keys()
            if generator_dependencies:
                errors.append(
                    f"{package_id}: runtime package depends on generator packages "
                    f"{sorted(generator_dependencies)!r}"
                )

            if package_id == NATIVE_IMPORT_PACKAGE:
                errors.extend(verify_native_import_package(archive, names, root))
            elif package_id in {
                "IoT-Driver.ABPlcRx",
                "IoT-Driver.ABPlcRx.Reactive",
            }:
                if NATIVE_IMPORT_PACKAGE not in dependency_ids:
                    errors.append(
                        f"{package_id}: must depend on {NATIVE_IMPORT_PACKAGE}"
                    )
                if UPSTREAM_NATIVE_IMPORT_PACKAGE in dependency_ids:
                    errors.append(
                        f"{package_id}: must not depend directly on "
                        f"{UPSTREAM_NATIVE_IMPORT_PACKAGE}"
                    )
                native_import_dependencies = [
                    element
                    for element in root.findall(".//{*}dependency")
                    if element.attrib.get("id") == NATIVE_IMPORT_PACKAGE
                ]
                if native_import_dependencies and not all(
                    package_version in dependency.attrib.get("version", "")
                    for dependency in native_import_dependencies
                ):
                    errors.append(
                        f"{package_id}: {NATIVE_IMPORT_PACKAGE} dependency must use "
                        f"the package release version {package_version}"
                    )
                dependency_groups = root.findall(
                    ".//{*}metadata/{*}dependencies/{*}group"
                )
                dependency_tfms = {
                    group.attrib.get("targetFramework", "")
                    for group in dependency_groups
                }
                if dependency_tfms != AB_RUNTIME_DEPENDENCY_TFMS:
                    errors.append(
                        f"{package_id}: dependency groups do not match package "
                        f"targets; missing "
                        f"{sorted(AB_RUNTIME_DEPENDENCY_TFMS - dependency_tfms)!r}, "
                        f"unexpected "
                        f"{sorted(dependency_tfms - AB_RUNTIME_DEPENDENCY_TFMS)!r}"
                    )
                for group in dependency_groups:
                    group_native_import_dependencies = [
                        dependency
                        for dependency in group.findall("{*}dependency")
                        if dependency.attrib.get("id") == NATIVE_IMPORT_PACKAGE
                    ]
                    if len(group_native_import_dependencies) != 1 or (
                        group_native_import_dependencies[0].attrib.get("version")
                        != package_version
                    ):
                        errors.append(
                            f"{package_id}: dependency group "
                            f"{group.attrib.get('targetFramework', '')} must contain "
                            f"exactly one {NATIVE_IMPORT_PACKAGE} {package_version} "
                            "dependency"
                        )

    return package_id, errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("package_directory", type=Path)
    args = parser.parse_args()

    packages = sorted(
        path
        for path in args.package_directory.glob("*.nupkg")
        if not path.name.endswith(".snupkg")
    )
    errors: list[str] = []
    package_ids: list[str] = []

    if not packages:
        errors.append(f"No .nupkg files found in {args.package_directory}")

    for package in packages:
        try:
            package_id, package_errors = verify_package(package)
            package_ids.append(package_id)
            errors.extend(package_errors)
        except (ElementTree.ParseError, ValueError, zipfile.BadZipFile) as exception:
            errors.append(str(exception))

    duplicates = sorted(
        package_id for package_id in set(package_ids) if package_ids.count(package_id) > 1
    )
    missing = sorted(EXPECTED_PACKAGES - set(package_ids))
    unexpected = sorted(set(package_ids) - EXPECTED_PACKAGES)

    if duplicates:
        errors.append(f"Duplicate package IDs: {duplicates!r}")
    if missing:
        errors.append(f"Missing package IDs: {missing!r}")
    if unexpected:
        errors.append(f"Unexpected package IDs: {unexpected!r}")

    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1

    print(
        f"Verified {len(package_ids)} packages: the stable {NATIVE_IMPORT_PACKAGE} "
        "wrapper reproduces upstream alpha.8 assets, both AB runtime packages use it, "
        "and all seven generators are standalone analyzer packages."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
