"""Pinned inference assets and dependency-free preparation/verification tools."""

import argparse
import hashlib
import os
from pathlib import Path
import re
import sys
import tempfile
import time
import urllib.error
import urllib.request

EFFNET_EMBEDDING_MODEL = "discogs-effnet-bs64-1.pb"
EFFNET_EMBEDDING_OUTPUT = "PartitionedCall:1"
EFFNET_GENRE_MODEL = "genre_discogs400-discogs-effnet-1.pb"
EFFNET_GENRE_META = "genre_discogs400-discogs-effnet-1.json"
MAEST_GENRE_MODEL = "discogs-maest-30s-pw-519l-2.pb"
MAEST_GENRE_META = "discogs-maest-30s-pw-519l-2.json"

MOOD_CLASSIFIERS = [
    {"name": name, "model": f"{name}-discogs-effnet-1.pb",
     "meta": f"{name}-discogs-effnet-1.json", "output": "model/Softmax"}
    for name in ("mood_aggressive", "mood_happy", "mood_party", "mood_relaxed",
                 "mood_sad", "mood_acoustic", "voice_instrumental")
]

ASSET_ROOT = Path(__file__).resolve().parent


def required_model_files(backbone):
    if backbone not in ("effnet", "maest"):
        raise ValueError("BACKBONE must be 'effnet' or 'maest'.")

    files = {EFFNET_EMBEDDING_MODEL}
    for classifier in MOOD_CLASSIFIERS:
        files.update((classifier["model"], classifier["meta"]))

    if backbone == "effnet":
        files.update((EFFNET_GENRE_MODEL, EFFNET_GENRE_META))
    else:
        files.update((MAEST_GENRE_MODEL, MAEST_GENRE_META))

    return files


def load_assets(backbone, asset_root=ASSET_ROOT):
    checksum_path = asset_root / "models.sha256"
    if not checksum_path.exists():
        checksum_path = asset_root / "models" / "models.sha256"

    checksums = {}
    for line in checksum_path.read_text().splitlines():
        parts = line.split()
        if (len(parts) != 2 or not re.fullmatch(r"[a-fA-F0-9]{64}", parts[0])
                or not re.fullmatch(r"[A-Za-z0-9_.-]+", parts[1])
                or parts[1] in (".", "..") or parts[1] in checksums):
            raise ValueError(f"Invalid or duplicate pinned checksum in {checksum_path}")
        checksums[parts[1]] = parts[0].lower()

    assets = []
    seen = set()
    for line in (asset_root / "model-requirements.tsv").read_text().splitlines():
        if not line.strip() or line.startswith("#"):
            continue

        parts = line.split()
        if len(parts) != 4:
            raise ValueError("Invalid model requirement")
        group, filename, length, download_path = parts

        if (group not in ("shared", "effnet", "maest")
                or not re.fullmatch(r"[A-Za-z0-9_.-]+", filename)
                or filename in (".", "..") or filename in seen
                or not length.isascii() or not length.isdigit() or int(length) <= 0
                or any(not re.fullmatch(r"[A-Za-z0-9_-][A-Za-z0-9_.-]*", part)
                       or part in (".", "..") for part in download_path.split("/"))
                or download_path.split("/")[-1] != filename
                or filename not in checksums):
            raise ValueError(f"Invalid or unpinned model requirement: {filename}")

        seen.add(filename)
        if group in ("shared", backbone):
            assets.append((filename, int(length), checksums[filename], download_path))

    if {asset[0] for asset in assets} != required_model_files(backbone):
        raise ValueError(f"Pinned requirements do not match actual {backbone} inference dependencies")

    return assets


def is_verified(path, length, checksum):
    if path.is_symlink():
        raise ValueError(f"Refusing symbolic link in model path: {path}")
    if not path.is_file() or path.stat().st_size != length:
        return False

    with path.open("rb") as source:
        actual = hashlib.file_digest(source, "sha256").hexdigest()
    return actual == checksum


def verify_models(model_dir, backbone, asset_root=ASSET_ROOT):
    for filename, length, checksum, _ in load_assets(backbone, asset_root):
        if not is_verified(model_dir / filename, length, checksum):
            raise ValueError(f"Missing/corrupt required {backbone} model: {filename} in {model_dir}")


def prepare_models(model_dir, backbone, source_dir, allow_download=False, asset_root=ASSET_ROOT):
    assets = load_assets(backbone, asset_root)
    if model_dir.is_symlink() or model_dir.parent.is_symlink():
        raise ValueError(f"Refusing symbolic link in model cache: {model_dir}")
    model_dir.mkdir(parents=True, exist_ok=True)
    model_dir.chmod(0o755)

    lock_path = model_dir / ".prepare.lock"
    if lock_path.is_symlink():
        raise ValueError(f"Refusing symbolic link in model cache: {lock_path}")
    with lock_path.open("a+b") as preparation_lock:
        if os.name != "nt":
            import fcntl
            fcntl.flock(preparation_lock.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        _prepare_assets(model_dir, source_dir, allow_download, assets)


def _prepare_assets(model_dir, source_dir, allow_download, assets):

    for filename, length, checksum, download_path in assets:
        destination = model_dir / filename
        if is_verified(destination, length, checksum):
            destination.chmod(0o644)
            print(f"Reused verified cache: {filename}", flush=True)
            continue

        source_path = source_dir / filename
        if source_path.is_symlink():
            raise ValueError(f"Refusing symbolic link in model source: {source_path}")
        if not source_path.is_file() and not allow_download:
            raise FileNotFoundError(f"Missing existing model {source_path}; downloads are disabled. "
                                    "Supply a verified local source or explicitly pass --download.")

        for attempt in range(1, 4):
            temporary = None
            try:
                with tempfile.NamedTemporaryFile(prefix=f".{filename}.", suffix=".partial",
                                                 dir=model_dir, delete=False) as output:
                    temporary = Path(output.name)
                    if source_path.is_file():
                        source = source_path.open("rb")
                        print(f"Copying existing model: {filename}", flush=True)
                    else:
                        source = urllib.request.urlopen(
                            f"https://essentia.upf.edu/models/{download_path}", timeout=60)
                        print(f"Downloading model: {filename} (attempt {attempt}/3)", flush=True)

                    with source:
                        copied = 0
                        while chunk := source.read(131072):
                            copied += len(chunk)
                            if copied > length:
                                raise ValueError(f"Model {filename} exceeds its pinned size")
                            output.write(chunk)
                        output.flush()
                        os.fsync(output.fileno())

                if not is_verified(temporary, length, checksum):
                    raise ValueError(f"SHA-256/size verification failed for {filename}; verified cache retained")
                temporary.chmod(0o644)
                if destination.is_symlink():
                    raise ValueError(f"Refusing symbolic link in model cache: {destination}")
                temporary.replace(destination)
                print(f"Verified model ready: {filename} ({length} bytes)", flush=True)
                break
            except (OSError, urllib.error.URLError) as error:
                if attempt == 3 or isinstance(error, urllib.error.HTTPError) and error.code < 500 and error.code not in (408, 429):
                    raise
                print(f"Retrying {filename}: {error}", file=sys.stderr, flush=True)
                time.sleep(attempt)
            finally:
                if temporary is not None:
                    temporary.unlink(missing_ok=True)

    manifest = model_dir / "models.sha256"
    if manifest.is_symlink():
        raise ValueError(f"Refusing symbolic link in model cache: {manifest}")
    with tempfile.NamedTemporaryFile(mode="w", prefix=".manifest.", dir=model_dir, delete=False) as output:
        temporary = Path(output.name)
        try:
            output.write("".join(f"{checksum}  {filename}\n" for filename, _, checksum, _ in assets))
            output.flush()
            os.fsync(output.fileno())
            temporary.chmod(0o644)
            temporary.replace(manifest)
        finally:
            temporary.unlink(missing_ok=True)


def runtime_backbone(arguments):
    selected = os.environ.get("BACKBONE")
    for index, argument in enumerate(arguments):
        candidate = None
        if argument == "--backbone" and index + 1 < len(arguments):
            candidate = arguments[index + 1]
        elif argument.startswith("--backbone="):
            candidate = argument.split("=", 1)[1]

        if candidate:
            if selected and selected != candidate:
                raise ValueError("BACKBONE conflicts with the requested command backbone")
            selected = candidate

    required_model_files(selected)
    return selected


def main():
    if len(sys.argv) > 1 and sys.argv[1] == "runtime-verify":
        pinned_manifest = ASSET_ROOT / "models.sha256"
        if not pinned_manifest.is_file() or pinned_manifest.is_symlink():
            raise ValueError(f"Image-owned pinned manifest {pinned_manifest} is missing or unsafe")
        backbone = runtime_backbone(sys.argv[2:])
        model_dir = Path(os.environ.get("ESSENTIA_MODEL_DIR", "/app/models"))
        verify_models(model_dir, backbone)
        print(f"Pinned {backbone} model checksums OK.", file=sys.stderr)
        return

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("operation", choices=("prepare", "verify"))
    parser.add_argument("--model-dir", type=Path, required=True)
    parser.add_argument("--backbone", choices=("effnet", "maest"), required=True)
    parser.add_argument("--source-dir", type=Path, default=ASSET_ROOT / "models")
    parser.add_argument("--download", action="store_true", help="explicitly permit missing model downloads")
    arguments = parser.parse_args()

    if arguments.operation == "verify":
        verify_models(arguments.model_dir, arguments.backbone)
    else:
        prepare_models(arguments.model_dir, arguments.backbone, arguments.source_dir, arguments.download)


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, urllib.error.URLError) as error:
        print(f"FATAL: {error}", file=sys.stderr)
        sys.exit(1)
