#!/usr/bin/env python3
"""Exercise the real installer frame and cancellation in a disposable Linux PTY."""

import argparse
import errno
import fcntl
import json
import os
from pathlib import Path
import pty
import re
import select
import shutil
import signal
import struct
import subprocess
import tempfile
import termios
import time


ANSI = re.compile(rb"\x1b(?:\[[0-?]*[ -/]*[@-~]|\][^\x07]*(?:\x07|\x1b\\))")


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def version_key(path):
    return tuple(int(number) for number in re.findall(r"\d+", path.name))


def compile_harness(repo, scratch):
    dotnet = shutil.which("dotnet")
    require(dotnet is not None, "An installed .NET 10 SDK is required; this script never installs prerequisites.")
    dotnet_root = Path(dotnet).resolve().parent
    compilers = sorted(dotnet_root.glob("sdk/10.*/Roslyn/bincore/csc.dll"), key=lambda path: version_key(path.parents[2]))
    references = sorted((dotnet_root / "packs/Microsoft.NETCore.App.Ref").glob("10.*/ref/net10.0"),
                        key=lambda path: version_key(path.parents[1]))
    require(bool(compilers) and bool(references), "The installed SDK must include Roslyn and the .NET 10 reference pack.")
    output = repo / "Builder/bin/Debug/net10.0"

    for name in ("Builder.dll", "Builder.runtimeconfig.json", "Builder.deps.json", "Spectre.Console.dll", "Spectre.Console.Ansi.dll"):
        source = output / name
        require(source.is_file(), f"Current compiled Builder output is required: {source}")
        shutil.copy2(source, scratch / name)

    harness = scratch / "TerminalSmokeHarness.dll"
    command = [dotnet, str(compilers[-1]), "-nologo", "-target:exe", "-langversion:preview", "-nullable:enable",
               "-define:TERMINAL_SMOKE_HARNESS", "-out:" + str(harness)]
    command.extend("-r:" + str(path) for path in sorted(references[-1].glob("*.dll")))
    command.extend("-r:" + str(scratch / name) for name in ("Builder.dll", "Spectre.Console.dll", "Spectre.Console.Ansi.dll"))
    command.append(str(Path(__file__).with_name("TerminalSmokeHarness.cs")))
    environment = dict(os.environ, DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1", DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
    subprocess.run(command, cwd=repo, env=environment, check=True, timeout=30)

    return [dotnet, "exec", "--runtimeconfig", str(scratch / "Builder.runtimeconfig.json"),
            "--depsfile", str(scratch / "Builder.deps.json"), str(harness)]


def running(pid):
    try:
        state = Path(f"/proc/{pid}/stat").read_text().rsplit(")", 1)[1].split()[0]
        return state not in ("Z", "X")
    except (FileNotFoundError, ProcessLookupError):
        return False


def descendants(parent):
    children = {}

    for entry in Path("/proc").iterdir():
        if not entry.name.isdigit():
            continue

        try:
            fields = (entry / "stat").read_text().rsplit(")", 1)[1].split()
            children.setdefault(int(fields[1]), set()).add(int(entry.name))
        except (FileNotFoundError, ProcessLookupError, PermissionError):
            continue

    found = set()
    pending = [parent]

    while pending:
        current = pending.pop()
        unseen = children.get(current, set()) - found
        found.update(unseen)
        pending.extend(unseen)

    return found


def run_mode(command, repo, scratch, mode):
    target = scratch / mode
    preferences = target / "preferences"
    preferences.mkdir(parents=True)
    environment = dict(os.environ, TERM="xterm-256color", COLORTERM="truecolor", XDG_CONFIG_HOME=str(preferences),
                       DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1", DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
    pid, master = pty.fork()

    if pid == 0:
        os.chdir(repo)
        fcntl.ioctl(0, termios.TIOCSWINSZ, struct.pack("HHHH", 30, 120, 0, 0))
        os.execvpe(command[0], command + [mode, str(target)], environment)

    os.set_blocking(master, False)
    output = bytearray()
    exit_status = None
    observed_children = set()

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

    def wait_until(predicate, label, timeout=10, permit_exit=False):
        deadline = time.monotonic() + timeout

        while time.monotonic() < deadline:
            pump()

            if predicate():
                return

            if exit_status is not None and not permit_exit:
                break

        tail = ANSI.sub(b"", output[-3000:]).decode("utf-8", errors="replace")
        themes = [str(path.relative_to(target)) + ":" + path.read_text() for path in target.rglob("theme.json")]
        raise RuntimeError(f"{mode}: {label} failed (exit {exit_status}, Aurora frames {output.count(b'Aurora')}, "
                           f"preferences {themes}). Terminal tail:\n{tail}")

    try:
        wait_until(lambda: (target / "child-sleep.pid").exists() and b"Building Alexandria" in ANSI.sub(b"", output),
                   "real child build startup", timeout=20)
        require(b"\x1b[?1049h" in output, f"{mode}: the live frame did not enter the alternate terminal buffer.")
        require(b"Papyrus" in ANSI.sub(b"", output), f"{mode}: the original theme was not rendered.")
        observed_children.update(descendants(pid))
        observed_children.update(int((target / name).read_text().strip()) for name in ("child-shell.pid", "child-sleep.pid"))
        theme_start = len(output)
        os.write(master, b"t")
        theme_file = preferences / "alexandria-installer/theme.json"

        def theme_changed():
            if not theme_file.exists() or b"Aurora" not in ANSI.sub(b"", output[theme_start:]):
                return False

            try:
                return json.loads(theme_file.read_text()).get("theme") == "aurora"
            except json.JSONDecodeError:
                return False

        wait_until(theme_changed, "theme switching while the child is running")
        narrow_start = len(output)
        fcntl.ioctl(master, termios.TIOCSWINSZ, struct.pack("HHHH", 18, 70, 0, 0))
        os.kill(pid, signal.SIGWINCH)
        wait_until(lambda: b"\x1b[2J" in output[narrow_start:] and b"Installing Alexandria" in ANSI.sub(b"", output[narrow_start:]),
                   "narrow terminal repaint")
        wide_start = len(output)
        fcntl.ioctl(master, termios.TIOCSWINSZ, struct.pack("HHHH", 30, 100, 0, 0))
        os.kill(pid, signal.SIGWINCH)
        wait_until(lambda: b"\x1b[2J" in output[wide_start:] and b"Aurora" in ANSI.sub(b"", output[wide_start:]),
                   "wide terminal repaint")
        observed_children.update(descendants(pid))
        os.write(master, b"\x03")
        wait_until(lambda: exit_status is not None, "Ctrl+C cancellation", permit_exit=True)

        for _ in range(5):
            pump()

        require(exit_status == 130, f"{mode}: cancellation returned {exit_status}, expected 130.")
        require(b"\x1b[?1049l" in output, f"{mode}: cancellation did not restore the terminal alternate buffer.")
        require(b"\x1b[?25h" in output, f"{mode}: cancellation did not restore the cursor.")
        plain = ANSI.sub(b"", output)
        require(b"What now?" not in plain and b"Installation stopped" not in plain,
                f"{mode}: cancellation incorrectly entered failure recovery.")
        wait_until(lambda: not any(running(child) for child in observed_children), "child process tree termination", permit_exit=True)
        print(f"PASS {mode}: live theme change, narrow/wide repaint, Ctrl+C exit 130, terminal restored, child tree terminated.")
    finally:
        if exit_status is None:
            try:
                os.killpg(pid, signal.SIGKILL)
            except ProcessLookupError:
                pass

            os.waitpid(pid, 0)

        for child in observed_children:
            if running(child):
                try:
                    os.kill(child, signal.SIGKILL)
                except ProcessLookupError:
                    pass

        os.close(master)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[3])
    arguments = parser.parse_args()
    repo = arguments.repo.resolve()
    require(os.name == "posix" and Path("/proc").exists(), "This smoke test requires a Linux terminal and /proc.")

    with tempfile.TemporaryDirectory(prefix="alexandria-terminal-smoke-") as temporary:
        scratch = Path(temporary)
        command = compile_harness(repo, scratch)

        for mode in ("silent", "noisy"):
            run_mode(command, repo, scratch, mode)


if __name__ == "__main__":
    main()
