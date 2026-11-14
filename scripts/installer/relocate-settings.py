#!/usr/bin/env python3
"""Prepare a settings copy for relocation; never controls Docker or deletes the original."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def reject_links(path):
    for item in (path, *path.parents):
        if item.is_symlink():
            raise ValueError("Symbolic links are not supported for relocation.")
    if path.is_dir():
        for item in path.rglob("*"):
            if item.is_symlink():
                raise ValueError("Symbolic links are not supported for relocation.")


def artifact_path(root, name):
    relative = Path(name)
    if relative.is_absolute() or ".." in relative.parts:
        raise ValueError("Unsafe artifact path in generation checkpoint.")
    return root / relative


def relocate(settings, destination, source):
    settings, destination, source = map(lambda value: Path(os.path.abspath(value)),
                                        (settings, destination, source))
    for path in (settings, destination, source):
        reject_links(path)
    if destination.exists():
        raise ValueError("Destination already exists; it has not been changed.")
    if settings in destination.parents or destination in settings.parents:
        raise ValueError("Settings folders must not contain each other.")
    if source == destination or source in destination.parents or destination in source.parents:
        raise ValueError("Source and settings folders must be separate.")

    metadata = json.loads((settings / "alexandria-config.json").read_text())
    marker = json.loads((settings / ".alexandria-generation.json").read_text())
    checkpoint_config = marker["Configuration"]
    if (metadata.get("SchemaVersion") != 2 or marker.get("SchemaVersion") != 1
            or metadata.get("Stage") != "complete" or marker.get("Stage") != "complete"
            or metadata != checkpoint_config
            or metadata.get("GenerationId") != marker.get("GenerationId")
            or metadata.get("InstallPath") != str(settings)):
        raise ValueError("Only a complete, matching current installation can be relocated.")

    hashes = marker["ArtifactHashes"]
    if not hashes or ".env" not in hashes or "docker-compose.yml" not in hashes:
        raise ValueError("The checkpoint is missing required artifact hashes.")
    for name, expected in hashes.items():
        if sha256(artifact_path(settings, name)) != expected.upper():
            raise ValueError("An artifact differs from its checkpoint; repair it before relocating.")

    old_source = Path(metadata["SourceRoot"])
    reject_links(old_source)
    if not old_source.is_dir() or not source.is_dir():
        raise ValueError("Both source folders must exist.")
    for item in old_source.rglob("*"):
        if item.is_file():
            copy = source / item.relative_to(old_source)
            if not copy.is_file() or sha256(copy) != sha256(item):
                raise ValueError("The destination source must contain an unchanged copy of the original source.")

    destination.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    staging = Path(tempfile.mkdtemp(prefix=".alexandria-relocate-", dir=destination.parent))
    try:
        shutil.copytree(settings, staging, dirs_exist_ok=True, copy_function=shutil.copy2)
        os.chmod(staging, 0o700)

        for config in (metadata, checkpoint_config):
            config["InstallPath"] = str(destination)
            config["SourceRoot"] = str(source)
            if config.get("ModelSourceDirectory"):
                config["ModelSourceDirectory"] = str(destination / "models")

        for name in ("docker-compose.yml", "INSTALLATION.txt", "nginx/CLIENT-TRUST.txt"):
            path = staging / name
            if path.exists():
                content = path.read_text()
                path.write_text(content.replace(str(old_source), str(source))
                                .replace(str(settings), str(destination)))

        for name in hashes:
            hashes[name] = sha256(artifact_path(staging, name))

        for name, value in (("alexandria-config.json", metadata),
                            (".alexandria-generation.json", marker)):
            (staging / name).write_text(json.dumps(value, indent=2) + "\n")
            os.chmod(staging / name, 0o600)

        for item in settings.rglob("*"):
            if item.is_file() and item.relative_to(settings).as_posix() not in (
                    "alexandria-config.json", ".alexandria-generation.json", "docker-compose.yml",
                    "INSTALLATION.txt", "nginx/CLIENT-TRUST.txt"):
                if sha256(item) != sha256(staging / item.relative_to(settings)):
                    raise ValueError("An unchanged settings file failed verification.")

        staging.rename(destination)
    except BaseException:
        shutil.rmtree(staging)
        raise

    return {"settings": str(destination), "source": str(source),
            "project": metadata["ProjectName"], "generation": marker["GenerationId"],
            "artifacts_verified": len(hashes), "original_preserved": True}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--settings", required=True)
    parser.add_argument("--destination", required=True)
    parser.add_argument("--source", required=True, help="Unchanged copy of the original source in its permanent folder")
    args = parser.parse_args()
    try:
        print(json.dumps(relocate(args.settings, args.destination, args.source), indent=2))
    except (ValueError, OSError, KeyError) as error:
        parser.exit(1, f"Relocation stopped: {error}\n")
