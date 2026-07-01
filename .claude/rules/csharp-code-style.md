---
description: "C# code style — language idioms, naming, null checks, magic values"
applies_when: "C# projects (any stack)"
alwaysApply: true
paths: ["**/*.cs"]
---

# C# Code Style

> **Language version:** some idioms below require a recent C# compiler. Check the project's effective `<LangVersion>` (and target framework) before applying them, and fall back to the older form when the project is pinned to an earlier version. Required version is noted in parentheses where it matters.

## Enforcement

The deterministic rules below are enforced by the companion `.editorconfig` (copy it to the project root) via the built-in .NET (Roslyn) analyzers. They surface in the IDE; to fail the build on violations, also set `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` in the project. Version-gated rules are inert on older compilers — analyzers respect `<LangVersion>` automatically.

| Enforced by `.editorconfig` | Judgment / review only |
| --- | --- |
| Braces (IDE0011), expression-bodied members (IDE0022/0025/0026/0027), switch expressions (IDE0066), collection expressions (IDE0300–0305), primary constructors (IDE0290), file-scoped namespaces (IDE0161), var-when-apparent (IDE0007), target-typed new (IDE0090), unused/sorted usings (IDE0005), is-null/pattern matching (IDE0041/0078/0083), naming (IDE1006) | LINQ readability balance, "enums/constants over magic values", record/required for DTOs, one type per file |

Two rules the built-in analyzers don't fully cover and which therefore stay review/judgment (or need a third-party analyzer — Roslynator / SonarAnalyzer): flipping `== null` → `is null`, and preferring `nameof()` over string literals.

## Language features

- Primary constructors on classes/structs (**C# 12 / .NET 8**) over an explicit constructor plus private backing fields. Inject dependencies through the primary constructor and use them directly. (Records have had primary constructors since C# 9.) *(enforced: IDE0290)*
- Expression-bodied members for simple methods and properties (methods C# 6, properties/accessors C# 7 — effectively universal). *(enforced: IDE0022/0025/0026/0027)*
- Switch expressions with pattern matching over `if`/`switch` statement chains for value-producing conditionals (**C# 8**; some pattern forms need C# 9+). *(enforced: IDE0066)*
- Collection expressions (`[..]`) for collection initialization (**C# 12 / .NET 8**); use a collection initializer (`new[] { ... }` / `new List<T> { ... }`) on earlier versions. *(enforced: IDE0300–0305)*

## Namespaces

- File-scoped namespace declarations (`namespace Foo;`) over block-scoped (`namespace Foo { ... }`) (**C# 10 / .NET 6**); use the block form on earlier versions. *(enforced: IDE0161)*

```csharp
namespace Acme.Orders;

public class OrderService(IOrderRepository repository, IClock clock)
{
    public Task<Order> GetAsync(int id) => repository.GetAsync(id);

    public string Describe(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Awaiting payment",
        OrderStatus.Shipped => "On its way",
        _ => "Unknown",
    };

    private static readonly string[] DefaultTags = ["new", "unpaid"];
}
```

## Using directives

- Remove unused `using` directives and sort `System.*` namespaces first. *(enforced: IDE0005 + `dotnet_sort_system_directives_first`)*

> IDE0005 ("remove unnecessary usings") only reports at build time when `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is set — a known Roslyn limitation. It always works live in the IDE.

## Local variables and `new`

- Say the type once. Use `var` when the right-hand side already makes the type obvious (`new`, casts, a factory whose name carries the type); write the explicit type when the value's type isn't clear from the expression. *(enforced: IDE0007 for the apparent case; the inverse nudge IDE0008 is left silent — explicit-vs-var elsewhere is judgment)*
- Target-typed `new()` when the type is stated on the left — field/property/variable declarations, `return`s (**C# 9 / .NET 5**). *(enforced: IDE0090)*

```csharp
var order = new Order();                        // type apparent from the right
List<int> ids = new();                          // type stated on the left
PaymentResult result = ProcessPayment(order);   // explicit: type not obvious
```

## Immutable DTOs

- Prefer `record` (**C# 9**) for immutable, value-like data, with `init`-only or `required` members (**C# 11**) instead of a hand-written constructor plus readonly fields. *(review only)*

```csharp
public record OrderDto
{
    public required int Id { get; init; }
    public required string Customer { get; init; }
    public decimal Total { get; init; }
}
```

## File organization

- One top-level type per file, with the file named after the type. *(review only — StyleCop SA1402 if a third-party analyzer is in use)*

## LINQ readability

Neither LINQ nor loops is the default — pick whichever reads more clearly for the case at hand. Use LINQ for simple, declarative filters and projections where it is the shortest clear expression. When a chain grows complex (many stages, nested lambdas, branching, or hard to step through in a debugger), break it into named intermediate variables or a `foreach`. Optimize for the reader and the debugger, not for the fewest lines.

## Null checks

- Use `is null` / `is not null`, not `== null` / `!= null`. (`is null` is C# 7; `is not null` requires **C# 9** — use `!(x is null)` on earlier versions.) *(partially enforced: IDE0041/0078/0083; the `== null` → `is null` flip needs a third-party analyzer)*

## Braces

- Always use braces, even for single-statement `if`/`else`/`for`/`foreach` blocks. *(enforced: IDE0011)*

```csharp
if (order is null)
{
    return NotFound();
}
```

## Naming

*(enforced: IDE1006 — casing + `I`-prefix only; the rest below is review/judgment)*

- PascalCase for types, methods, properties, and constants.
- camelCase for locals and parameters.
- Interfaces prefixed with `I` (`IOrderRepository`).
- No abbreviations except ones read as words (`URL`, `SSN`, `Id`) — `user`, not `usr`.
- Match the name to its type — a `TestSuiteRun` is `lastTestSuiteRun`, not `lastRun` or `history`; an ambiguous name forces the reader to re-derive the type.
- Name the value, not the thing it counts/relates to — a loop counter is `retryCount` (it holds a count), not `retries` or `attempts`.

## Magic values

- Prefer enums or named constants over repeated string/number literals.
- Use `nameof()` instead of string literals that track a member name.

```csharp
ArgumentNullException.ThrowIfNull(order, nameof(order));
```
