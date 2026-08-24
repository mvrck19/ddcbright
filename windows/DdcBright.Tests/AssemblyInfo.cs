// Debouncer/timer-based tests use real wall-clock delays; running test
// classes in parallel (xunit's default) contends for thread-pool timer
// callbacks and made them flaky specifically on constrained (2-core)
// CI runners even though they passed reliably locally. The whole suite
// runs in well under a second either way, so sequential costs nothing.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
