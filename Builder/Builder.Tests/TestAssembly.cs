using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Builder.Tests;

// Tradeoff: test execution is fully sequential by design.
//
// The render tests verify TUI output by swapping the process-global
// AnsiConsole.Console over to a Spectre.Console.Testing.TestConsole for the
// duration of a step, then asserting on what was captured. That global is
// process-wide mutable state: under parallel execution two fixtures swap and
// restore it out of order, a step's output lands on another fixture's
// console, and captures come back empty (observed 2026-08-25: Success render
// test failed only in the full run, passed in isolation).
//
// We accept losing parallel speed because the whole suite stays well under a
// second. If parallel tests are ever wanted back, the real fix is injecting
// IAnsiConsole through the steps instead of calling static AnsiConsole.* -
// a deliberate refactor of every step and component, not a quick toggle.
public static class TestAssembly;
