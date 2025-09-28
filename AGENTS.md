# Repository Guidelines
## Project Structure & Module Organization
OilLeak targets Unity 2022.3.8f1. Gameplay assets live under `Assets`: `Scripts` holds the runtime code split into feature folders (`Core`, `GameModes`, `UI`, `Resupply`), `Scenes/GameScene.unity` is the primary entry point, and art/audio live in `Materials`, `Models`, `Textures`, and `Audio`. Shared configuration resides in `ProjectSettings` and package manifests in `Packages/manifest.json`. Planning artifacts and handoffs are tracked in `project/docs`, `project/plans`, and `project/issues`; update these alongside code so agents can trace context quickly.

## Build, Test, and Development Commands
- `<UnityInstall>/Editor/Unity.exe -batchmode -projectPath "." -quit -logFile Logs/Verify.log` confirms the project imports cleanly after package updates.
- `<UnityInstall>/Editor/Unity.exe -batchmode -projectPath "." -runTests -testPlatform PlayMode -logFile Logs/PlayMode.log` runs Unity Test Framework play-mode suites; swap `EditMode` when authoring editor-only checks.
- `dotnet build Assembly-CSharp.csproj -warnaserror` performs a fast compile sanity check and surfaces C# warnings before pushing.

## Coding Style & Naming Conventions
Use four-space indentation and braces on new lines, matching existing classes such as `Assets/Scripts/Core/GameController.cs`. Follow PascalCase for classes and public members, camelCase for fields and locals, and prefix serialized private fields with `[SerializeField]` if exposure is required. Keep diagnostics via `Debug.Log` structured and scoped (e.g., `[SystemName] Message`) to ease log filtering. When adding editor scripts, place them inside `Assets/Scripts/Editor` to keep assemblies separated.

## Testing Guidelines
Author play-mode tests under `Assets/Tests/PlayMode` and edit-mode tests under `Assets/Tests/EditMode`; mirror the runtime namespace so coverage is traceable. Ensure new systems include a deterministic harness—mock `ScriptableObject` dependencies and assert against `SessionStats` values rather than scene state. Target at least smoke coverage for new feature branches and attach resulting `Logs/*.log` artifacts to review notes. For manual validation, document reproduction steps or GIFs in `project/handoffs`.

## Commit & Pull Request Guidelines
Commits follow Conventional Commits (`feat:`, `fix:`, `refactor:`) as seen in recent history; keep scopes short (`feat(resupply): …`) and squash noisy WIP changes locally. Every pull request should summarize gameplay impact, list validation steps (tests, builds), link Jira/GitHub issues, and include relevant screenshots or clips for UX-visible changes. Tag the owning feature lead in `project/coord` when cross-team coordination is required.

## Agent Tooling Notes (PowerShell + ripgrep)

We run on Windows PowerShell (pwsh). Quoting and regex pipes (`|`) inside `-Command` are easy to mis-specify. Use these patterns to search safely and avoid parser/regex errors:

- Prefer fixed-string search with multiple expressions to avoid regex quoting:
  - `pwsh -Command rg -n -S -F -e "KeyCode.R" -e "GetKey(" -e "GetKeyDown(" Assets/Scripts`

- If you need regex, single-quote the pattern so PowerShell doesn’t eat backslashes, and avoid unescaped pipes:
  - `pwsh -Command rg -n 'GameFlowState|ShowRoundOverUI' Assets/Scripts`

- When using regex alternation inside double quotes, escape pipes with PowerShell’s backtick (\`):
  - `pwsh -Command "rg -n 'KeyCode\.R\`|GetKeyDown\(' Assets/Scripts"`

- For large-file reads, use chunked reads to respect output limits (≤250 lines):
  - `pwsh -Command Get-Content -Path 'path\to\file.cs' | Select-Object -First 200`
  - `pwsh -Command Get-Content -Path 'path\to\file.cs' | Select-Object -Skip 200 -First 200`

- As a fallback, you can use PowerShell’s own grep:
  - `pwsh -Command "Get-ChildItem Assets/Scripts -Recurse -Filter *.cs | Select-String -n -Pattern 'KeyCode\.R','GameFlowState'"`

- Don’t mix shell features that conflict with pwsh parsing (e.g., Python heredocs). Stick to pwsh-native commands and `rg`.

These conventions prevent errors like “An empty pipe element is not allowed” (PowerShell misreading `|`) and “regex parse error: unclosed group” (shell consuming backslashes/parentheses before `rg` sees them.
