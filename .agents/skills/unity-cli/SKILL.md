---
name: unity-cli
description: Resolve the Unity CLI binary and translate project paths before running any Unity CLI command, on WSL, Windows, or macOS. Use whenever a task calls for `unity` — building, running tests, opening a project, driving a live Editor with `unity command` / `eval`, installing editors or modules, or configuring the Unity MCP server — and especially when the shell is WSL but the Unity Editor is a Windows installation. Handles binary discovery, path-form translation, and the preflight checks that decide whether an Editor command can run at all.
allowed-tools:
  - Bash
---

# Unity CLI — environment resolution layer

Unity's own `unity-cli` skill documents command syntax assuming the CLI, the Editor, and the
project all live on one operating system. That assumption breaks the moment the shell and the
Editor are on different sides of a WSL boundary, which is the common setup on Windows machines.

This skill runs **first**. It decides three things — which binary, which path form, and whether
an Editor command can run at all — and then hands off to the official command syntax.

Skipping it produces failures that look like missing projects or broken installs but are neither.

## Step 1 — Resolve the binary

Choose player build output outside `Library/`. Unity 2022.3 rejects an output
such as `Library/ArtelSdkUpdateVerification/player/Game.exe` with
`ArgumentException: Invalid build path`, and `BuildPipeline.BuildPlayer` can
return null. Use an ignored `Build/` directory for players, keep logs in
`Library/` if needed, and check the report for null before reading its summary.

Run the resolver and export what it prints:

```bash
source .claude/skills/unity-cli/scripts/unity-env.sh
```

It sets `UNITY_BIN` and `UNITY_PLATFORM` (`wsl`, `windows`, `mac`, or `linux`). To do it by hand,
work down this table:

| Shell | Where the binary is |
|---|---|
| **WSL** | `/mnt/c/Program Files/Unity Hub/resources/cli/unity.exe` — the Windows Hub bundles it. WSL runs `.exe` transparently through binfmt_misc, so it is callable like any Linux command. A Linux-native `unity` in `~/.local/bin` may also exist; see *Which binary on WSL* below. |
| **Windows** | `unity` is on `PATH` once Unity Hub is installed. Fall back to the same `resources/cli/unity.exe` under the Hub install directory. |
| **macOS** | `which unity` first. Unity Hub bundles a CLI inside its app bundle, but do not hardcode that path — locate it with `find "/Applications/Unity Hub.app" -name unity -type f 2>/dev/null` and use what you find. |
| **Linux (native)** | `which unity`. If absent, install: `curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh \| UNITY_CLI_CHANNEL=beta bash` — it lands in `~/.local/bin/unity`. |

If `unity editors` returns an empty list, do not conclude no Editor is installed.
Inspect an existing project's `Library/Bee/*.dag.json` or compiler response files
for `Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll`, then verify the
corresponding `Editor/Unity.exe` and project version before using it. On the
material-hit work the registry was empty while the project's 2022.3.34f1 Editor
was installed at `C:/Tools/2022.3.34f1/Editor/Unity.exe`.

Verify before relying on it:

```bash
"$UNITY_BIN" --version --no-banner
```

### Which binary on WSL

Two can coexist and they are **not** interchangeable:

- **Windows `unity.exe`** — drives Windows Editors. Can only open projects on the Windows
  filesystem (NTFS). This is the one that can do real GUI and Editor work on a typical
  Windows-plus-WSL machine.
- **Linux-native `unity`** — drives Linux Editors. Can only open projects on the Linux
  filesystem. Requires installing a Linux Editor (multi-GB) and a separate Unity sign-in, since
  the two CLIs keep separate credential stores.

Pick by where the project lives, not by which is more convenient to type.

## Step 2 — Translate the project path

**On WSL this is the single most common failure.** The Windows `unity.exe` resolves a
Unix-style argument against the WSL filesystem's own UNC root, not against the drive you meant:

```bash
# WRONG — silently resolves to \\wsl.localhost\Ubuntu\mnt\c\Users\...
unity test /mnt/c/Users/me/Projects/Game
#   Error: not a Unity project (ProjectVersion.txt not found at
#   \\wsl.localhost\Ubuntu\mnt\c\Users\me\Projects\Game)

# RIGHT — pass a Windows path
unity test 'C:\Users\me\Projects\Game'
unity test "$(wslpath -w /mnt/c/Users/me/Projects/Game)"
```

Rule: **on `UNITY_PLATFORM=wsl`, every path argument goes through `wslpath -w` first.** This
covers the project argument and every path-valued option — `--output`, `--junit-output`,
`--logfile`, `--build-path`. A relative path or `.` is equally wrong; convert it too.

On `windows`, `mac`, and `linux`, pass paths as-is.

## Step 3 — Preflight before any Editor-spawning command

`build`, `test`, `run`, and `open` launch an Editor process. Three conditions block them, and
each produces an error that reads like something else. Check them in this order.

### 3a. Case-sensitive filesystem — hard block, no workaround

Unity refuses to open a project on a case-sensitive volume:

```
Fatal Error! The project is on case sensitive file system.
Case sensitive file systems are not supported at the moment.
```

This is deliberate on Unity's part, not a bug and not WSL-specific — a case-sensitive macOS
volume is refused the same way. The exit code is **21**.

It means a **Windows or macOS Editor can never open a project on WSL's ext4**, no matter how the
path is spelled. `fsutil file setCaseSensitiveInfo` does not help: it applies to NTFS
directories, not to the ext4 filesystem inside the WSL VM.

The consequences are structural, so decide the layout before running anything:

- Project on WSL ext4 → only a **Linux** Editor can open it.
- Project on NTFS (`/mnt/c/...`) → only a **Windows** Editor can open it, and the shell may still
  be WSL as long as paths are translated per Step 2.

### 3b. An Editor already has the project open

```
Aborting batchmode due to fatal error:
It looks like another Unity instance is running with this project open.
```

Batch mode cannot take the project lock. Close the GUI Editor, or drive the running Editor
instead — see *Driving a live Editor* below.

**`unity status` does not detect this.** It reports only Editors connected through the
`com.unity.pipeline` package; a plain GUI Editor is invisible to it. Empty `unity status` output
is not evidence that no Editor is running.

### 3c. License and sign-in

```bash
"$UNITY_BIN" auth status --no-banner
"$UNITY_BIN" license --no-banner
```

Credential stores are per-CLI-installation, so a WSL Linux CLI is signed out even when the
Windows CLI on the same machine is signed in. Batch mode fails with licensing errors when the
active install has no entitlement.

## Driving a live Editor

When an Editor is open and the project has `com.unity.pipeline` (Unity 6.0+), prefer driving it
over spawning a second one — it also sidesteps the lock in 3b:

```bash
"$UNITY_BIN" status                 # look for state "ready"
"$UNITY_BIN" command                # list the commands THIS Editor exposes — never assume names
"$UNITY_BIN" command eval 'new UnityEngine.GameObject("Probe");'
```

If `status` and `command` will not connect while an Editor is clearly running, suspect **Safe
Mode**: a C# compile error keeps the Pipeline package from loading. Confirm with
`unity pipeline list`, then fix the compile errors and restart the Editor. Do not conclude "no
Editor" and fall back to hand-editing files.

Note this project pins Unity 2022.3, which predates `com.unity.pipeline`. Live-Editor driving is
unavailable here; the repo uses `LocalPackages/com.coplaydev.unity-mcp` instead. See the
`unity-mcp-orchestrator` skill.

## Working across a split checkout

When the code lives on WSL and the Editor needs NTFS, two checkouts of the same repository are
the workable arrangement. Keep the roles separate:

- **WSL checkout** — canonical. Code editing, search, git. Fast.
- **Windows checkout** — GUI only. Scenes, prefabs, inspector work, and Editor-spawning CLI
  commands.

Sync both ways. Code flows WSL → Windows; scene and prefab changes flow Windows → WSL. Treating
it as one-directional means working against a stale scene.

Do not "solve" this by moving the canonical checkout to `/mnt/c`. Cross-filesystem I/O is
catastrophically slow for tool-driven work — measured on this machine, writing 2000 small files
took **0.02s on ext4 versus 13.4s on `/mnt/c`**, and reads were comparably slow. Every recursive
grep and every `git status` pays that.

## Reference

Command syntax, flags, and CI recipes: [official-skill.md](references/official-skill.md) explains
how to fetch Unity's own `unity-cli` skill and what it covers.

Platform constraints in one table, plus the beta bugs worth knowing:
[platform-matrix.md](references/platform-matrix.md).

## Notes

- Always pass `--no-banner` in scripts, and `--format json` whenever output will be parsed.
- Read failures from **stdout**, not stderr: a failed command still writes a full JSON envelope
  with `success: false`. Branch on `success`, never on `data` being empty.
- Exit codes: `0` success, `1` general, `2` bad arguments, `3` auth failure, `4` precondition,
  `6` command failure, `21` case-sensitive filesystem, `130` SIGINT, `143` SIGTERM.
