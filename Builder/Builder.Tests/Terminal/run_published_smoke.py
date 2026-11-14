#!/usr/bin/env python3
"""Drive the shipped native installer in a real PTY, outside its source checkout.

The default stops before deployment. --deploy explicitly builds and starts a fresh
installation and leaves it running for acceptance checks. This script never
installs prerequisites or removes Docker resources.
"""

import argparse
import errno
import fcntl
import hashlib
import json
import os
from pathlib import Path
import pty
import re
import secrets
import select
import signal
import struct
import tempfile
import termios
import time


ANSI = re.compile(rb"\x1b(?:\[[0-?]*[ -/]*[@-~]|\][^\x07]*(?:\x07|\x1b\\))")


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def verify_bundle(binary, source):
    require(binary.read_bytes()[:4] == b"\x7fELF", "Expected a published native ELF executable, not a managed launcher.")
    expected = source / "Builder/Templates"
    actual = binary.parent / "Templates"
    files = [path.relative_to(expected) for path in expected.rglob("*") if path.is_file()]
    require(bool(files), "Source template manifest is empty.")
    for relative in files:
        shipped = actual / relative
        require(shipped.is_file(), f"Missing shipped template: {relative}")
        require(hashlib.sha256(shipped.read_bytes()).digest() == hashlib.sha256((expected / relative).read_bytes()).digest(),
                f"Shipped template differs from source: {relative}")
    print(f"PASS package: native ELF and all {len(files)} templates match source.", flush=True)


def run(arguments):
    binary = arguments.binary.resolve()
    source = arguments.source.resolve()
    target = arguments.target.resolve()
    require(binary.is_file() and os.access(binary, os.X_OK), "Published executable is missing or not executable.")
    saved = None
    before = {}
    if arguments.repair:
        require((target / "alexandria-config.json").is_file(), "Repair smoke requires an existing installer-generated target.")
        saved = json.loads((target / "alexandria-config.json").read_text())
        require(Path(saved["SourceRoot"]).resolve() == source, "Repair source must match this installation's saved source.")
        before = {path.relative_to(target): hashlib.sha256(path.read_bytes()).digest()
                  for path in target.rglob("*") if path.is_file()}
    else:
        require(not target.exists(), "Choose a new disposable target; this smoke check preserves existing folders.")
    require(" " in str(source) and " " in str(target), "Acceptance source and target paths must both contain spaces.")
    if arguments.selection != "core":
        require(arguments.model_source is not None, "Audio selections require an existing verified local model cache.")
    verify_bundle(binary, source)
    password = "P6!aA1" + secrets.token_hex(16)

    with tempfile.TemporaryDirectory(prefix="alexandria native launch ") as scratch_name:
        scratch = Path(scratch_name)
        environment = dict(os.environ, TERM="xterm-256color", COLORTERM="truecolor",
                           XDG_CONFIG_HOME=str(scratch / "preferences"), DOTNET_ROOT=str(scratch / "no-dotnet"),
                           DOTNET_MULTILEVEL_LOOKUP="0", DOTNET_CLI_TELEMETRY_OPTOUT="1")
        pid, master = pty.fork()
        if pid == 0:
            os.chdir(scratch)
            fcntl.ioctl(0, termios.TIOCSWINSZ, struct.pack("HHHH", 60, 160, 0, 0))
            os.execvpe(str(binary), [str(binary)], environment)

        os.set_blocking(master, False)
        output = bytearray()
        exit_status = None
        checkpoint = 0

        def pump():
            nonlocal exit_status
            readable, _, _ = select.select([master], [], [], 0.05)
            if readable:
                try:
                    chunk = os.read(master, 65536)
                    output.extend(chunk)
                    if b"\x1b[6n" in chunk:
                        os.write(master, b"\x1b[1;1R")
                except OSError as error:
                    if error.errno != errno.EIO:
                        raise
            if exit_status is None:
                waited, status = os.waitpid(pid, os.WNOHANG)
                if waited:
                    exit_status = os.waitstatus_to_exitcode(status)

        def wait_for(label, timeout=30):
            deadline = time.monotonic() + timeout
            needle = label.encode()
            while time.monotonic() < deadline:
                pump()
                plain = ANSI.sub(b"", output[max(checkpoint, len(output) - 262144):])
                if needle in plain:
                    return
                if exit_status is not None or b"What now?" in plain or b"Fix the issue, then press R" in plain:
                    break
            tail = ANSI.sub(b"", output[-10000:]).decode("utf-8", errors="replace").replace(password, "[REDACTED]")
            raise RuntimeError(f"Native wizard did not reach {label!r}; exit={exit_status}. Terminal tail:\n{tail}")

        def send(value=b"\r"):
            nonlocal checkpoint
            checkpoint = len(output)
            os.write(master, value)

        def text(value):
            send(b"\x15" + str(value).encode() + b"\r")

        try:
            wait_for("Main menu")
            send()
            wait_for("Alexandria source folder")
            text(source)
            wait_for("Installation settings folder")
            text(target)
            wait_for("Review the resolved folders")
            send()
            wait_for("Space toggles, Enter confirms")
            toggle_ids = ("media-processing", "document-previews", "adaptive-streaming", "autotagging", "lyrics", "monitoring")
            current = set(saved["Features"]["Enabled"]) if saved else set(toggle_ids[:5]) | {"audio-tagging-fast"}
            desired = set() if arguments.selection == "core" else set(toggle_ids if arguments.selection.startswith("max-") else toggle_ids[:5])
            keys = bytearray()
            for index, feature in enumerate(toggle_ids):
                if (feature in current) != (feature in desired):
                    keys.extend(b" ")
                if index < len(toggle_ids) - 1:
                    keys.extend(b"\x1b[B")
            send(bytes(keys) + b"\r")
            wait_for("Audio analysis model")
            old_model = 1 if "audio-tagging-fast" in current else 2 if "audio-tagging-deep" in current else 0
            new_model = 0 if arguments.selection == "core" else 2 if arguments.selection == "max-maest" else 1
            send((b"\x1b[B" if new_model >= old_model else b"\x1b[A") * abs(new_model - old_model) + b"\r")
            wait_for("All good? Press Enter", timeout=90)
            send()
            wait_for("Storage for your files" if saved else "Garage capacity in GB")
            send()
            wait_for("LAN address or DNS name")
            text(arguments.host)
            wait_for("Choose another port")
            send(b"\x1b[B\r")
            wait_for("Choose the port your devices will use")
            text(arguments.port)
            wait_for("Press Enter to continue; Esc returns")
            send()
            wait_for("Generated certificates use a local CA")
            send()
            if arguments.selection != "core":
                wait_for("Existing model folder")
                text(arguments.model_source.resolve())
            if saved:
                wait_for("Existing admin account")
                send()
            else:
                wait_for("Email address")
                text("native-admin@example.invalid")
                wait_for("Username")
                send()
                wait_for("6+ characters")
                text(password)
                wait_for("Repeat password")
                text(password)
            wait_for("Almost done - quick check")
            if saved:
                require(all((target / path).is_file() and hashlib.sha256((target / path).read_bytes()).digest() == digest
                            for path, digest in before.items()), "Pre-deployment review changed existing installation artifacts.")
            else:
                require(not target.exists() or not any(target.iterdir()), "Pre-deployment navigation unexpectedly wrote installation artifacts.")
            print("PASS native wizard: external working directory, spaced source/target, shipped templates, all setup screens.", flush=True)
            if arguments.deploy:
                send()
                wait_for("Alexandria is up and running", timeout=arguments.deploy_timeout)
                send()
            else:
                send(b"\x03")
            deadline = time.monotonic() + 20
            while exit_status is None and time.monotonic() < deadline:
                pump()
            require(exit_status == (0 if arguments.deploy else 130), f"Unexpected native installer exit: {exit_status}")
            for _ in range(3):
                pump()
            require(b"\x1b[?1049l" in output and b"\x1b[?25h" in output, "Native installer did not restore terminal state.")
            if arguments.deploy:
                config = json.loads((target / "alexandria-config.json").read_text())
                require(config["Stage"] == "complete", "Native installation checkpoint is not complete.")
                require((target / "INSTALLATION.txt").is_file(), "Native installation summary is missing.")
                require(not (target / "init-output/garage-credentials.env").exists(),
                        "Completed installation retained the temporary Garage credential handoff.")
                require((target / "init-output/garage-credentials.persisted").read_text() == config["GenerationId"] + "\n",
                        "Completed installation is missing its generation-bound credential persistence receipt.")
                if saved:
                    require(config["ProjectName"] == saved["ProjectName"] and config["InstalledAt"] == saved["InstalledAt"],
                            "Repair changed established installation identity.")
                    require(all(hashlib.sha256((target / path).read_bytes()).digest() == digest for path, digest in before.items()
                                if str(path).startswith("nginx/certs/")), "Repair rotated certificate/trust material.")
                print(f"PASS native deployment: exit 0, checkpoint complete, settings {target}, project {config['ProjectName']}.", flush=True)
            else:
                if saved:
                    require(all((target / path).is_file() and hashlib.sha256((target / path).read_bytes()).digest() == digest
                                for path, digest in before.items()), "Cancellation before repair changed installation artifacts.")
                else:
                    require(not target.exists() or not any(target.iterdir()), "Cancellation before deployment wrote installation artifacts.")
                print("PASS native cancellation: exit 130, terminal restored, no generated configuration or services.", flush=True)
        finally:
            if exit_status is None:
                try:
                    os.killpg(pid, signal.SIGINT)
                    deadline = time.monotonic() + 10
                    while exit_status is None and time.monotonic() < deadline:
                        pump()
                    if exit_status is None:
                        os.killpg(pid, signal.SIGKILL)
                        os.waitpid(pid, 0)
                except ProcessLookupError:
                    pass
            os.close(master)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--binary", type=Path, required=True)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--target", type=Path, required=True)
    parser.add_argument("--host", required=True, help="LAN IPv4 or DNS public host; loopback is deliberately unsupported.")
    parser.add_argument("--port", type=int, default=10443)
    parser.add_argument("--selection", choices=("core", "recommended", "max-effnet", "max-maest"), default="core")
    parser.add_argument("--model-source", type=Path)
    parser.add_argument("--deploy", action="store_true")
    parser.add_argument("--repair", action="store_true", help="Explicitly review an existing disposable installation; preserve its identity.")
    parser.add_argument("--deploy-timeout", type=int, default=1800)
    run(parser.parse_args())


if __name__ == "__main__":
    main()
