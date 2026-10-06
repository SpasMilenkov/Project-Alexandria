namespace Builder.Tests;

// Named collection grouping every test that swaps the process-global
// AnsiConsole. Combined with assembly-wide sequential execution this keeps
// render/flow tests deterministic (see TestAssembly.cs).
[CollectionDefinition(nameof(StaticConsoleTests))]
public sealed class StaticConsoleTests;
