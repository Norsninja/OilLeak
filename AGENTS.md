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
