# AGENTS.md

Rules for every session in this repository, human or agent. They are not style
preferences. Each one exists because breaking it destroys something the work depends
on, and each is stated with that consequence attached.

---

## 1. `Assets/` and `ProjectSettings/` are frozen

Not one line changes. `Assets/Src/Managers/LevelManager.cs` is the "before" half of a
comparison; cleaning it up deletes the evidence. An agent with write access to
`Assets/**` is misconfigured — that is a harness bug, not a judgement call.

**Enforcement:** write paths are scoped by the harness. Any diff touching `Assets/**`
or `ProjectSettings/**` fails the run.

## 2. Agents may not write to `core/tests/**`

This is the load-bearing guardrail. The standard way an agent loop manufactures a
green build is to edit the test until it agrees with the code. Blocking the path
removes the option rather than asking the agent not to take it.

Tests are changed by a human, in a separate commit, never in the same change as the
code they cover.

**Enforcement:** path-scoped write block in the harness; diff check in CI.

## 3. No new dependencies in `core/` without explicit approval

`Game2048.Core` builds with zero third-party packages. `Game2048.Cli` and
`Game2048.Legacy` too. Test projects may use the xUnit stack and nothing else.

**Enforcement:** the harness fails a run whose diff adds a `PackageReference` to any
`.csproj`.

## 4. `Game2048.Core` must not reference `UnityEngine`

Enforced by construction, not convention: Core is a `netstandard2.1` class library
with no Unity assemblies on its reference path, and the CLI runs it on plain .NET. A
`UnityEngine` using directive is a compile error, which is the point — the constraint
holds without anyone remembering it.

## 5. If a non-obvious line can't be explained, it doesn't ship

Applies to every agent-authored line. Agent output is cheap; review throughput is the
bottleneck. Code that survives review only because nobody understood it well enough
to object is worse than no code.

## 6. Don't apply patterns speculatively

Two interfaces are correct in this codebase:

- `IRng`, because deterministic replay requires injection and there are genuinely two
  implementations.
- The agent-invocation interface in `harness/`, because a recorded-transcript
  fallback is wanted.

Anything else with one implementation and no test seam is speculative. No
`IBoardFactory`, no strategy class per direction, no command hierarchy, no event bus.

The one abstraction that earns its place in Core is transpose/reverse normalisation,
so one `Move` implementation serves all four directions instead of four copies.

---

## Layout

```
Assets/                        FROZEN — the "before"
ProjectSettings/               FROZEN
core/
  src/Game2048.Core/           netstandard2.1, zero deps — the rules
  src/Game2048.Cli/            net8.0 console front-end
  src/Game2048.Legacy/         netstandard2.1, faithful port of the original bugs
  tests/Game2048.Core.Tests/   net8.0 xUnit — AGENTS MAY NOT WRITE HERE
harness/                       Go — runs tasks against core under guardrails
docs/                          EXTRACTION.md, FINDINGS.md, DELEGATION-LOG.md
```

## Verifying a change

```
dotnet build core/Game2048.sln
dotnet test  core/Game2048.sln
dotnet run --project core/src/Game2048.Cli -- --replay <file>
```

Sub-second, no editor. That is what makes an agent loop viable here: verification is
cheap enough to run on every iteration.
