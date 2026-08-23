using System.Diagnostics.Tracing;

namespace DdcBright.Tests;

public class DdcBrightEventSourceTests
{
    // EventSource.GenerateManifest(..., EventManifestOptions.Strict) throws
    // if the event definitions are malformed (ID collisions, mismatched
    // Task/Opcode pairing, bad argument shapes) -- catches that at test
    // time instead of only when someone tries to capture a real trace.
    // Built into the BCL, no extra package: EventSourceAnalyzer (which this
    // originally reached for) only ever shipped in Microsoft's archived,
    // long-unmaintained "Enterprise Library Semantic Logging" package, not
    // in anything actively maintained.
    [Fact]
    public void EventSource_ManifestIsWellFormed()
    {
        EventSource.GenerateManifest(typeof(DdcBrightEventSource), string.Empty, EventManifestOptions.Strict);
    }
}
