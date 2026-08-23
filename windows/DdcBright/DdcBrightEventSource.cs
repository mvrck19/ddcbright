using System.Diagnostics.Tracing;

namespace DdcBright;

/// <summary>
/// ETW provider for DdcBright's hot paths. Emits Start/Stop pairs rather
/// than single duration events -- the idiomatic EventSource pattern, used
/// throughout the BCL's own EventSources -- so tools like PerfView/WPA/
/// dotnet-trace correlate nested activities automatically (e.g. a
/// SchedulerTick activity containing the SetBrightness calls it triggered)
/// and derive duration themselves from the two timestamps.
///
/// Event IDs are part of this provider's on-disk contract: once shipped,
/// never renumber or reuse one -- a trace captured against an older build
/// would then get misdecoded against a newer provider.
///
/// TrayIconScrollHook is deliberately NOT instrumented here: its decision
/// logic runs synchronously inside a global WH_MOUSE_LL hook with an
/// existing &lt;50us/call budget (see TrayIconScrollHookTests/Benchmarks) --
/// not worth risking even EventSource's near-zero disabled-path cost there.
/// Scroll-triggered brightness changes already flow through the debouncer
/// onto a thread-pool thread and land on SetBrightness (App.xaml.cs), which
/// IS instrumented, so scroll-driven hardware writes are still fully
/// covered without touching the hook.
///
/// No IsEnabled() guards at call sites: every argument below is an
/// already-computed cheap value (int/long), and EventSource's generated
/// WriteEvent overloads already do a fast internal enablement check before
/// doing any real work -- that check IS the "near-zero cost when nothing is
/// listening" property. An explicit guard only earns its keep when
/// *computing* the arguments is itself expensive, which isn't the case here.
/// </summary>
[EventSource(Name = "DdcBright")]
internal sealed class DdcBrightEventSource : EventSource
{
    public static readonly DdcBrightEventSource Log = new();

    private DdcBrightEventSource()
    {
    }

    public static class Keywords
    {
        public const EventKeywords MonitorControl = (EventKeywords)0x1;
        public const EventKeywords Scheduler = (EventKeywords)0x2;
        public const EventKeywords AmbientLight = (EventKeywords)0x4;
    }

    public static class Tasks
    {
        public const EventTask SetBrightness = (EventTask)1;
        public const EventTask GetBrightness = (EventTask)2;
        public const EventTask SchedulerTick = (EventTask)3;
        public const EventTask ComputeLuma = (EventTask)4;
    }

    [Event(1, Task = Tasks.SetBrightness, Opcode = EventOpcode.Start, Keywords = Keywords.MonitorControl)]
    public void SetBrightnessStart(long monitorHandle, int percent) => WriteEvent(1, monitorHandle, (long)percent);

    [Event(2, Task = Tasks.SetBrightness, Opcode = EventOpcode.Stop, Keywords = Keywords.MonitorControl)]
    public void SetBrightnessStop() => WriteEvent(2);

    [Event(3, Task = Tasks.GetBrightness, Opcode = EventOpcode.Start, Keywords = Keywords.MonitorControl)]
    public void GetBrightnessStart(long monitorHandle) => WriteEvent(3, monitorHandle);

    [Event(4, Task = Tasks.GetBrightness, Opcode = EventOpcode.Stop, Keywords = Keywords.MonitorControl)]
    public void GetBrightnessStop(int result) => WriteEvent(4, result);

    [Event(5, Task = Tasks.SchedulerTick, Opcode = EventOpcode.Start, Keywords = Keywords.Scheduler)]
    public void SchedulerTickStart() => WriteEvent(5);

    [Event(6, Task = Tasks.SchedulerTick, Opcode = EventOpcode.Stop, Keywords = Keywords.Scheduler)]
    public void SchedulerTickStop() => WriteEvent(6);

    [Event(7, Task = Tasks.ComputeLuma, Opcode = EventOpcode.Start, Keywords = Keywords.AmbientLight)]
    public void ComputeLumaStart(int width, int height) => WriteEvent(7, width, height);

    [Event(8, Task = Tasks.ComputeLuma, Opcode = EventOpcode.Stop, Keywords = Keywords.AmbientLight)]
    public void ComputeLumaStop(int luma) => WriteEvent(8, luma);
}
