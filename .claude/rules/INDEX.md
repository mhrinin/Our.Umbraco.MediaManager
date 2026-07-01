# .NET Rule Index

Genericized C# rules — lift the relevant ones into a project's `.claude/rules/` (or `CLAUDE.md`). Each file has an `applies_when` field in its frontmatter; use this index to scan applicability without opening every file.

## Universal — C# projects

| Rule | Applies when |
| --- | --- |
| [csharp-code-style.md](csharp-code-style.md) | C# projects (any stack) |

Companion artifact: copy [`.editorconfig`](.editorconfig) to the C# project's repo root alongside `csharp-code-style.md` — it enforces the deterministic subset of the style (braces, expression-bodied members, switch/collection expressions, primary constructors, file-scoped namespaces, `var`/target-typed `new`, using hygiene, naming) via the built-in Roslyn analyzers.

## Frontend / Markup — always read for any HTML, SCSS, or Razor work

| Rule | Applies when |
| --- | --- |
| [MARKUP_RULES.md](MARKUP_RULES.md) | Every response — HTML, SCSS, Razor, or UI work |
| [markup-requirements.md](markup-requirements.md) | Every response — HTML, SCSS, Razor, or UI work |
| [frontend-rules.md](frontend-rules.md) | Every response — HTML, SCSS, Razor, or UI work |
