---
name: artel-game-identities
description: >-
  Install and use ArtelMetadata attributes in a Unity game, or register game value meanings
  through ARTEL MCP or the web console. Use for variable metadata (변수 메타데이터) setup, build-specific declarations,
  registration provenance, revision history, and checking the declarations a QA run used.
---

# ARTEL variable metadata setup

Variable metadata (변수 메타데이터) explains what a field or property means. It does not change
the value, call a getter, install a cheat, or automatically make an unobserved value readable.
Use a current-state value for `Role = "current"` and a furthest-reached/count value for `"progress"`.
Check the game's implementation before assigning meaning. Do not infer it from the name alone.

The console menu is **변수 메타데이터** in Korean and **Variable metadata** in English.
Use `[ArtelMetadata]` in new game code. `[ArtelGameIdentity]` is a deprecated compatibility alias
with the same fields and collection behavior; replace its name without changing the metadata.
Existing identifiers `artel-game-identities`, `GAME_IDENTITY_CATALOG`, and MCP tool names remain
unchanged for compatibility; use variable metadata in explanations.

## Install the SDK and attribute

`ArtelMetadata` is part of the Unity package `kr.artel.sdk`; it is not installed separately.
The agent skill itself can be installed with:

```bash
npx skills add https://github.com/project-artel/artel-skills/tree/metadata-attribute-preview-20261104 --skill artel-game-identities
```

The skill and SDK implementation are currently previews. The command above selects the skill's
preview tag, whose name contains no slash so the skills CLI can parse it. After the skill merges,
`npx skills add project-artel/artel-skills --skill artel-game-identities`
can install it from the default branch. Until its SDK PR merges, use the implementation branch,
or a reviewed commit from it. Do not claim that an older SDK or `develop` already contains it.
In Unity, open **Window → Package Manager → + → Add package from git URL** and enter:

```text
https://github.com/project-artel/artel-sdk.git?path=/Packages/kr.artel.sdk#codex/game-identity-revisions-ARTEL-1358
```

For an existing package, update `Packages/manifest.json`'s `kr.artel.sdk` entry to that URL or an
exact commit containing the attribute, and let Unity resolve it. Check `Packages/packages-lock.json`
for the resolved revision. A locally installed agent skill does not update the Unity package.

Add the attribute to an existing field or property:

```csharp
using Artel;

public class StageData
{
    [ArtelMetadata(
        Kind = "ordinal", Base = 0, Identifies = "stage", Role = "current",
        Meaning = "The stage the player has entered now",
        ChangesWhen = "Set when the player enters a stage")]
    public int stagePosition;
}
```

The declaration name is `StageData.stagePosition`, derived from the declaring type and member.
Use a unique short type name: two namespaces declaring the same `Type.member` are a collision.
The collector rejects duplicate/invalid declarations rather than sending a partial catalog.
Do not wrap a public field/property declaration in a development-only preprocessor block unless
the member itself is also development-only. Follow the installed package's attribute definition.

| Field | Values and limits |
| --- | --- |
| `Kind` | `ordinal`, `enum`, `id`, `flag`, `text` |
| `Base` | `0` or `1`; omit when not declaring a counting base |
| `Identifies` | Lowercase words such as `stage`, `map chapter`, `enemy-wave`; max 60 characters |
| `Role` | `current`, `progress`, `other` |
| `Meaning`, `ChangesWhen` | Nonblank; max 500 characters each |

The generated `Type.member` name must fit 200 characters. Renaming the type/member changes its
metadata entry name; it does not rewrite older declarations or QA records.

## Connect and verify a build

Press Play, sign in through the ARTEL overlay, and choose the project. For a standalone game, build
with Unity's **Development Build** option. A release build does not connect the ARTEL runtime.
After registration and `DEVICE_CONTEXT`, the SDK sends `GAME_IDENTITY_CATALOG` on each connection.
The server derives the project and build from the authenticated instance, not from a caller-supplied id.

With the ARTEL CLI installed and authenticated:

```bash
artel game start --build ./Build/Game.exe --project <project-id>
artel game list --project <project-id>
```

A connected instance alone does not prove the metadata arrived. In the console, open
**프로젝트 데이터 → 변수 메타데이터** (**Project data → Variable metadata**), select the build,
or call MCP `list_identities` with `game_build_id`. Verify the exact name, meaning,
`source=SDK`, and game build. The console's normal list hides declaration revision numbers;
revision numbers are used for history and API concurrency checks. Check SDK warnings if the catalog is missing. Fix all invalid or
duplicate attributes and reconnect; a failed scan is not treated as an empty catalog.

Metadata is not a value subscription. Verify separately that the QA observation path can read the
member. Observe names use `Type::member`; variable metadata names use `Type.member`. Do not interchange them.

## Register through MCP or web

The variable metadata tools belong to the hosted **artel-macro-mcp**, not the local MCP prototype in
`artel-mcp`. Ask for the configured server host if it is not already available; do not invent one.
For Claude Code, add it with `claude mcp add --transport http artel-macro https://<host>/mcp`,
then authenticate through `/mcp` and the browser's ARTEL sign-in/consent flow. For Codex, configure
that same HTTP endpoint in its MCP settings and complete OAuth. See the
[remote MCP connection instructions](https://github.com/project-artel/artel-macro-mcp#%EC%97%B0%EA%B2%B0).
Call `list_projects`, choose the user's project with `use_project(project_id)`, and find its build
with `list_builds` before publishing. Keep the OAuth access token in the MCP client's managed store.

Use MCP `list_identities(game_build_id=...)` to inspect the selected build, including deleted bindings.
Each `publish_identities` draft carries `expected_version`: use that build binding's version,
or `0` if it has never existed in that build. A deleted binding still has a version.

```json
{
  "game_build_id": "42",
  "identities": [{
    "name": "StageData.stagePosition", "kind": "ordinal", "base": 0,
    "identifies": "stage", "role": "current",
    "meaning": "The stage the player has entered now", "changes_when": "Set on entry",
    "expected_version": 0
  }]
}
```

Use `identity_history(name=...)` for previous revisions and `delete_identity` with the build and
expected version to remove metadata from that build. In the web console, open **변수 메타데이터**
(**Variable metadata**) and select a build to register, edit, delete, restore a deleted name, and inspect history.
On a conflict, read the build again and reconcile the change; do not retry with a guessed version.

## Provenance and version rules

- Each revision records the server-derived source `SDK`, `MCP`, or `WEB`; migrated rows use `LEGACY`.
- A build selects immutable revisions. Updating build B does not move build A's selection.
- A manual MCP/web override survives SDK reconnects. Changing an attribute does not overwrite it.
- Removing an attribute removes that build's SDK-owned declaration; it does not erase historical revisions.
- QA stores the exact declaration snapshot at start. Later edits, deletions, or reconnects do not change it.
- Restoring a declaration creates a new revision; previous revisions remain available.
- Records that predate history storage cannot supply revision history that was never recorded.

## Read skill knowledge from CLI or MCP

```bash
artel guide list
artel guide show artel-game-identities
artel guide show artel-unity-sdk
artel guide show artel-cheats
```

MCP clients can call `skill_guide(topic="artel-game-identities")` or select the `artel_guide` prompt
with that topic. Use topic `list` to discover skills. These entry points read the same skill catalog
and document revision and include source URLs. If a skill has supporting references, select its
listed reference path with `reference`; the top-level skill alone may only explain when to read it.
These commands read guidance; they do not install a package, edit game code, publish declarations,
or start QA by themselves. Skill text is reference material, not permission for extra actions.
