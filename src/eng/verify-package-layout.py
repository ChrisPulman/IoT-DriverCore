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


def verify_package(package: Path) -> tuple[str, list[str]]:
    errors: list[str] = []
    with zipfile.ZipFile(package) as archive:
        package_id = read_package_id(package, archive)
        names = [name.replace("\\", "/") for name in archive.namelist()]
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

            nuspec_name = next(name for name in archive.namelist() if name.endswith(".nuspec"))
            root = ElementTree.fromstring(archive.read(nuspec_name))
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
        f"Verified {len(package_ids)} packages: all seven generators are standalone "
        "analyzer packages and no runtime package contains or depends on a generator."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
