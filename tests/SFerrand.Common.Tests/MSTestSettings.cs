// Tests are independent (no shared mutable state, no fixed ports), so they run in parallel at method level.
// Anything that needs ordering or exclusive access must not be added to this assembly without revisiting this line.
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
