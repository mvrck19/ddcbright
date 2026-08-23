# DdcBright.Benchmarks

[BenchmarkDotNet](https://benchmarkdotnet.org/) microbenchmarks for the hot
paths in the Windows app -- most importantly `TrayIconScrollHookBenchmarks`,
which measures the callback logic that runs synchronously on Windows'
global low-level mouse input pipeline (see `TrayIconScrollHook.cs`). Slow
code there is a desktop-wide problem, not just this app's, so this is the
benchmark to check after touching that file.

These are **local/manual only** -- not run in CI. BenchmarkDotNet's
statistical benchmarks need a quiet, dedicated machine to produce trustworthy
numbers; shared CI runners are too noisy for that. CI only compiles this
project (`dotnet build ... -c Release`) so it can't silently bit-rot.

## Running

```powershell
dotnet run -c Release --project windows/DdcBright.Benchmarks
```

BenchmarkDotNet always requires a Release build -- it will refuse to run
against Debug. Pick a specific benchmark class interactively, or run
everything:

```powershell
dotnet run -c Release --project windows/DdcBright.Benchmarks -- --filter '*'
```

Results (timing + `[MemoryDiagnoser]` allocation stats) print to the console
and are also written as Markdown/HTML/CSV reports under
`BenchmarkDotNet.Artifacts/results/` next to the project.

## What's covered

- `TrayIconScrollHookBenchmarks` -- the tray-icon scroll hook's decision
  logic, both when the cursor is over the icon and when it isn't.
- `DebouncerBenchmarks` -- a burst of rapid `Trigger()` calls, modeling fast
  mouse-wheel scrolling.
- `LumaComputationBenchmarks` -- ambient-light webcam frame processing
  (`AmbientLightSensor.ComputeAverageLuma`), with allocation tracking since
  it runs on a 30s timer for as long as Ambient mode is active.

`MonitorControl`'s DDC/CI calls aren't benchmarked here: they're blocking
I2C bus operations against real monitor hardware, which a benchmark harness
(or CI) can't meaningfully exercise.
