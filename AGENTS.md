# Repository Guidelines

## Project Structure & Module Organization
This repository is a Unity WebGL client. Runtime code lives under `Assets/Scripts`, organized mostly by scene and domain: `LoginScene`, `LobbyScene`, `GameScene`, `ResultScene`, plus shared code in `Global` and `Data`. Scene assets are in `Assets/Scenes`, reusable prefabs in `Assets/Prefabs`, art/audio in `Assets/Art` and `Assets/Resources`, and localized content in `Assets/Localization`. Unity and package configuration live in `ProjectSettings/` and `Packages/`.

## Build, Test, and Development Commands
Open the project with Unity `2022.3.34f1`. Typical local work is done through the Unity Editor.

- `open -a Unity .` opens the project in Unity Hub/Editor on macOS.
- Build from the Editor using `BuildScript.BuildWebGL` or `BuildScript.BuildDevWebGL` in `Assets/Scripts/Editor/BuildScript.cs`.
- Example CLI build: `Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildDevWebGL -logFile -`

CI uses GitHub Actions:
- Push to `main` triggers `.github/workflows/deploy-test.yml`
- Push to `deploy` triggers `.github/workflows/deploy-itch.yml`

## Coding Style & Naming Conventions
Use C# with 4-space indentation and K&R braces, matching the existing codebase. Keep classes and public members in `PascalCase`, private fields in `camelCase`, and scene/domain folders aligned with namespaces where possible. Prefer descriptive MonoBehaviour names such as `GameSceneUIController` and keep shared infrastructure in `Assets/Scripts/Global`.

For visual styling, layout, colors, shapes, and typography guidelines, follow the [DESIGN.md](.agents/docs/DESIGN.md) design system spec.

`Global.Button.AsyncButtonBase.ButtonEvent` calls `OnClickButton` first and
locks the button afterwards, so a `ResetButton()` inside `OnClickButton` is
overwritten in the same frame and the button stays grey. A button that has to
stay pressable after a click — one that asks the user to press again to
confirm, for example — must wait a frame before calling `ResetButton()`.

Before changing what an existing button component does, find every scene and
prefab that uses it: `git grep <guid from its .cs.meta> -- '*.unity' '*.prefab'`.
The folder a script sits in does not tell you. `ResultScene.GotoLobbyButton` is
also the back button of `AdventuresScene`, `MagicBookScene` and
`ManageDeckScene`. #170 made it load `AdventureScene` after an adventure match,
and that left players unable to leave `AdventuresScene` for the lobby (#172).
When only one scene needs the new behavior, write a new component for that
scene, as `ResultContinueButton` does.

Follow [scene-space.md](.agents/docs/scene-space.md) before writing gameplay code that positions anything in the world or animates a `ServedObject`. The camera is tilted and sprites are billboarded to it, so `Vector3.up` and `Vector3.Distance` are the wrong tools for offsets and lengths measured off a sprite. That file also records that this project has no `Animator`.

Follow [json-payloads.md](.agents/docs/json-payloads.md) before adding a field to a server DTO, writing a `JsonConverter`, or typing a DTO field as an enum. `JsonCodec` registers `StringEnumConverter` for the whole client, so one unknown enum name from the server throws and the caller discards the entire response; Json.NET also serializes getter-only properties, so a cached property lands in the `PlayerPrefs` cache unless it is `[JsonIgnore]`.

Follow [localization.md](.agents/docs/localization.md) before adding or editing anything under `Assets/Localization`. The same magic is keyed by three different spellings of its server name, so adding one of them and stopping leaves another screen showing a raw key. The string tables are hand-edited YAML here because the Editor cannot run in this environment.

Follow [json-payloads.md](.agents/docs/json-payloads.md) before adding a field to a server DTO, writing a `JsonConverter`, or typing a DTO field as an enum. `JsonCodec` registers `StringEnumConverter` for the whole client, so one unknown enum name from the server throws and the caller discards the entire response; Json.NET also serializes getter-only properties, so a cached property lands in the `PlayerPrefs` cache unless it is `[JsonIgnore]`.

Follow [webgl-auth-hosting.md](.agents/docs/webgl-auth-hosting.md) before changing anything about WebGL login, refresh tokens, or where a WebGL build is published. The same build authenticates differently depending on the page it runs in: the refresh cookie is only stored when the page shares the account server's domain, so it works on `arcanecasters.theevilent.com` and is dropped on the `itch.io` and `github.io` addresses, and an origin outside the account server's CORS list fails at the preflight before any login happens.

Follow [hand-edited-assets.md](.agents/docs/hand-edited-assets.md) before changing a serialized type, renaming a serialized field, or deleting a `MonoBehaviour`. The Editor cannot run here, so `.asset`, `.prefab`, `.unity` and `.meta` files are edited as YAML by hand. A serialized enum stores its integer, so reordering one silently remaps every stored row, and deleting a script takes three edits across the script, its prefab and every scene that instances it.

## Testing Guidelines

Before adding a bulk ARTEL coroutine cheat, check the installed SDK's
`GameFunctionActions.CheatCoroutineLimitSeconds` (60 seconds in the pinned SDK).
Keep request timeouts and the whole operation below that limit, report partial
completion, and make retries read current server counts before granting more.
The all-magic grant needs one request per magic; a long loop can be stopped
after only some cards were granted, and replaying additive grants duplicates them.

Before changing in-game card hover, gameplay pause, or ping timing, read
[card-hover-pause.md](.agents/docs/card-hover-pause.md). Freezing scaled time
also freezes a scaled ping timer, while the authoritative server keeps running.
`com.unity.test-framework` is installed, but there are no dedicated `Tests` assemblies in the current tree. For new automated tests, create Edit Mode or Play Mode test assemblies under `Assets/Tests` and name files `*Tests.cs`. At minimum, validate scene flow and WebGL-specific behavior manually in the Editor before opening a PR.

`WordOnline.Tests.EditMode` cannot reference `Assembly-CSharp` (no asmdef can), so a test there
cannot name a game type such as `IProjectile` or `ServedObject`. `prefab.GetComponent<IProjectile>()`
in a test fails to compile in the Editor, and the Roslyn check in `hand-edited-assets.md` does not
build `Assets/Tests`, so it does not catch it. Match by type name instead, as
`PveCardUnitPrefabTests.HasComponentNamed` does, or with `GetType().GetInterface("IProjectile")`.

Before using `dotnet build Assembly-CSharp.csproj` to validate a newly added script, confirm the generated project file contains that script. Unity does not always regenerate the project file while another Editor instance has the project open, and the build then reports misleading missing-type errors only at existing call sites. Refresh/regenerate from Unity, or temporarily add the missing `Compile` entries for the check and remove them before committing.

## Commit & Pull Request Guidelines
Recent history favors short Conventional Commit subjects such as `feat: ...`, `refactor: ...`, and `chore: ...`. Keep the subject imperative and specific. Pull requests should include a short behavior summary, linked issue or ticket, test notes, and screenshots or short clips for UI or scene changes.
Branch names should follow `<issue-label>/<issue-num>`, for example `feature/299`.

## Agent Workflow
When work starts from a new request, follow this order unless the user explicitly says otherwise:
1. Create or identify the GitHub issue for the task.
2. Create or switch to the branch that matches `<issue-label>/<issue-num>`.
3. Implement and verify the change.
4. Record what the work taught you, per the section below.
5. Open a pull request that references the issue and includes test notes.

## Project Skills

This repository keeps its own skills under `.agents/skills/`. Read the one that covers the task before starting. An agent that only auto-loads skills from its own home directory does not see these, so open the file by path.

- `.agents/skills/create-game-audio/SKILL.md` — converts natural-language game audio requests into ElevenLabs Sound Effects prompts and parameters, generates review batches, and integrates approved clips into Unity; use for UI, card, impact, weapon, footstep, magic, or ambience sound-effect work.
- `.agents/skills/pr-media/SKILL.md` — puts screenshots, renders, or diagrams into a pull request by committing them under `docs/pr-media/<issue-num>/` and linking them by commit SHA; use for any PR that a picture would make easier to review.
- `.agents/skills/magick/SKILL.md` — ImageMagick CLI reference for inspecting, converting, resizing, cropping, compositing, and batch-processing images with the `magick` command.
- `.agents/skills/make-game-art/SKILL.md` — creates, edits, reviews, or standardizes WordOnline visual assets under one project-wide art style; use for character, creature, faction, building, environment, VFX, icon, sprite, or art-direction work.
- `.agents/skills/make-motion-comic/SKILL.md` — turns one comic-page image into a playable single-board Unity motion-comic scene, with separate panel artwork, editable dialogue, comic framing and verified scene handoff.
- `.agents/skills/record-gif/SKILL.md` — records actual app or Unity playback and converts local video/frame recordings into an animated GIF under a measured size limit.
- `.agents/skills/make-magic/SKILL.md` — adds or scaffolds a new magic's client-side localization text and generated icon/sprite art for server-derived magic data; use `make-prefab` separately for prefab wiring.
- `.agents/skills/make-prefab/SKILL.md` — adds or validates a runtime magic prefab and icon sprite under `Assets/Resources/Prefabs` and `Assets/Resources/Game`.
- `.agents/skills/plan-review/SKILL.md` — plan review protocol that runs fast and medium first-pass reviews then a heavy verification pass before folding findings back into the plan; use before non-trivial implementation.
- `.agents/skills/unity-cli/SKILL.md` — resolves the Unity CLI binary and translates project paths before running Unity commands across WSL, Windows, or macOS, including when the shell and the Editor are on different operating systems.
- `.agents/skills/unity-mcp-orchestrator/SKILL.md` — orchestrates the Unity Editor via MCP tools for creating or modifying GameObjects, editing scripts, managing scenes, and running tests.
- `.agents/skills/unity-ui-prefabs/SKILL.md` — enforces inspecting existing scenes first and reusing `Assets/Prefabs/UI` prefabs instead of hand-building UI; use for Unity UI scenes, panels, buttons, list items, or HUD/overlay work.
- `.agents/skills/writing-plan/SKILL.md` — generates a work plan markdown file under `.plan/general/` or `.plan/issues/` with strict naming conventions, optionally linked to a GitHub issue; use when asked to write a plan or starting non-trivial work.

## Record What the Work Teaches You

Whenever work in this repository surfaces something an agent could not have
predicted from the code, write it into the instructions in the same pull request.
Do this actively — the moment you think "I would have avoided an hour if I had
known that", it belongs in a file, not only in the pull request description.

Record it when you hit any of these:

- A check that looked sufficient and was not.
- A convention that is invisible in the code and only shows up as a wrong result.
- A tool, generator or asset pipeline that fails in a specific, repeatable way.
- A trap in an existing file that the next person will copy without noticing.
- A wrong assumption you made, and what would have caught it sooner.

Where it goes:

- Guidance for a specific task, into the matching skill under `.agents/skills/`.
- A property of the codebase or the scene, into a topic file under
  `.agents/docs/`, linked from this file with an explicit imperative.
- A short rule that applies everywhere, inline in this file.

Write the trigger, the required action, and how to tell it went wrong. State the
concrete failure that prompted the rule — a rule whose reason is visible survives
review. Prefer extending a nearby section over starting a new file.

Every issue and pull request must set an assignee and a label. Do not leave either blank.

- Assignee: `--assignee @me`.
- Label: use the same value as the branch prefix. Check available labels with `gh label list`; do not invent a new label when none fit.
- Do not attach a project.
- When GitHub CLI authentication appears invalid inside a sandbox but the user says their session is valid, request escalated execution and retry `gh` with the user's session credentials before asking them to re-authenticate.

```bash
gh issue create --title "..." --body "..." --assignee @me --label documentation
gh pr create --base <base> --title "..." --body "..." --assignee @me --label documentation
```

Confirm the metadata after creation:

```bash
gh issue view <issue-number> --json assignees,labels
gh pr view <pr-number> --json assignees,labels
```

For Unity Editor automation through MCP, use the project skill at
`.agents/skills/unity-mcp-orchestrator/SKILL.md`.

## Configuration Notes

Read [developer-playground.md](.agents/docs/developer-playground.md) before changing
the editor playground or build scene/asset selection. UNITY_EDITOR only removes
code; even its MonoScript asset entered a real player build. Use the constrained
host assembly plus Editor bridge described there, and verify packed assets as
well as scene/Resources/Addressables dependencies.

Before adding, assigning or removing gameplay sounds, read
`.agents/docs/game-audio.md` and use the central catalog/player ownership path.

Before changing magic preview playback or registrations, read
`.agents/docs/magic-preview.md` for damage-status and recording validation traps.
Before changing preview loading or caches, read `.agents/docs/server-magic-previews.md`.
`DEV_BUILD` controls development server routing for WebGL builds. Avoid editing generated Unity metadata by hand unless the change is intentional, and keep `ProjectSettings/ProjectVersion.txt` in sync with the Unity version used for the change.

## Versioning

Before changing desktop builds or packaging, read `Tools/DesktopUpdater/README.md`.
The macOS executable follows `productName`, not the output `.app` filename.
Normalize the built executable and `CFBundleExecutable` before signing; changing
`productName` instead moves existing preferences and auth storage.

`PlayerSettings.bundleVersion`, serialized as `bundleVersion` in
`ProjectSettings/ProjectSettings.asset`, is the client's single version source.
Do not bump it in a pull request. The monorepo `deploy` skill bumps it once per
promotion: it commits `chore(release): WordOnlineClient vX.Y.Z` to `main`, merges
`main` into `deploy`, then tags and releases `vX.Y.Z` on the merge commit. The
level comes from the Conventional Commit messages promoted in that release:
MAJOR for a `!` marker or a `BREAKING CHANGE` trailer, MINOR for `feat:`, PATCH
otherwise, so write accurate commit types.

Do not maintain a second client version value.

## Code Review Rules

- Write all code review titles, descriptions, comments, and summaries in Korean.
- Preserve code identifiers, file paths, commands, error messages, and technical terms in their original form.
- For each finding, concisely explain the cause, impact, and suggested fix.

## Writing Style

Follow [`.agents/docs/writing-style.md`](.agents/docs/writing-style.md) for chat
replies, reviews, plans, handoffs, issues, commit titles and bodies, pull
requests, release notes, source comments, and all frontend copy (including
localization, errors, labels, and accessibility text).
Its wording and title rules take precedence over older style examples;
preserve project-specific technical constraints and workflows.
