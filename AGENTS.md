# Repository Guidelines

## Project Structure & Module Organization

This is a Unity 6000.4.10f1 project. Keep Unity-authored project files under `Assets/`, `Packages/`, and `ProjectSettings/`.

- `Assets/Scenes/` contains general Unity scenes, including `SampleScene.unity`.
- `Assets/LipSyncTest/` contains the lip-sync sample: `Audio/`, `Models/`, `Prefabs/`, `Profiles/`, `Scenes/`, `Timeline/`, and C# scripts.
- `Assets/LipSyncTest/Scripts/Runtime/` is for runtime components; `Assets/LipSyncTest/Scripts/Editor/` is for editor-only tooling.
- `Assets/McpValidation/Editor/` contains Unity MCP validation editor code.
- `Docs/` contains repository documentation.

Track `.meta` files with their assets. Do not commit generated Unity folders such as `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, `Recordings/`, or `UserSettings/`.

## Build, Test, and Development Commands

- `git lfs install && git lfs pull`: fetch VRM, audio, texture, and other binary assets managed by Git LFS.
- Open the repository in Unity Editor `6000.4.10f1` to restore packages from `Packages/manifest.json`.
- `Unity -batchmode -projectPath "$(pwd)" -quit -runTests -testPlatform EditMode`: run Unity Edit Mode tests when tests are present.
- `Unity -batchmode -projectPath "$(pwd)" -quit -runTests -testPlatform PlayMode`: run Play Mode tests when needed.

CI runs `.github/workflows/repository-hygiene.yml`, which verifies Git LFS availability, required Unity project files, tracked `.meta` files, and absence of tracked generated folders.

## Coding Style & Naming Conventions

Use C# with 4-space indentation. Follow existing Unity conventions: `PascalCase` for types, public methods, and properties; `camelCase` for locals and parameters; descriptive class names ending in their role, such as `Vrm10BakedLipSyncDriver` or `TimelineRecorderBatchRunner`. Place editor-only scripts in an `Editor/` folder and use editor namespaces such as `UnityTry.McpValidation.Editor`.

## Testing Guidelines

Use the Unity Test Framework (`com.unity.test-framework`). Put Edit Mode tests under an `Editor/` test folder and Play Mode tests under runtime test folders. Name test classes after the behavior under test, and use clear test method names such as `DriverAppliesBakedBlendShapeWeights`.

## Commit & Pull Request Guidelines

Recent history uses short conventional-style subjects, for example `feat: validate Unity MCP integration`, `ci: add Unity repository hygiene workflow`, and `chore: launch Unity MCP via stdio`. Keep commits focused and use prefixes such as `feat:`, `fix:`, `ci:`, or `chore:`.

Write PR descriptions in Japanese. Add `close #<issue番号>` at the start of the PR description only when the user explicitly specified an issue number. Include relevant validation results and screenshots or recordings for visual Unity changes.

## Security & Configuration Tips

Do not commit machine-local Unity state or credentials. Package restoration needs access to `packages.unity.com`, `registry.npmjs.org`, `github.com`, and a working `git` command because UniVRM and Unity MCP dependencies are Git URL packages.
