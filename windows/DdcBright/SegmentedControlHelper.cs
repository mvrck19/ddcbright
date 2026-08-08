using System.Windows.Controls.Primitives;

namespace DdcBright;

/// <summary>
/// Wires a row of ToggleButtons (styled via App.xaml's SegmentToggleStyle)
/// into an exclusive-selection "segmented control". Shared by the
/// Off/Schedule/Ambient and Instant/Gradual pickers in both windows so the
/// selection logic exists in exactly one place.
/// </summary>
internal static class SegmentedControlHelper
{
    public static void WireExclusive(IReadOnlyList<ToggleButton> buttons, Action<int> onSelect)
    {
        for (var i = 0; i < buttons.Count; i++)
        {
            var index = i;
            buttons[i].Click += (_, _) =>
            {
                if (buttons[index].IsChecked == true)
                {
                    SetSelected(buttons, index);
                    onSelect(index);
                }
                else
                {
                    // Clicking the already-selected segment: keep it checked.
                    buttons[index].IsChecked = true;
                }
            };
        }
    }

    public static void SetSelected(IReadOnlyList<ToggleButton> buttons, int index)
    {
        for (var i = 0; i < buttons.Count; i++)
            buttons[i].IsChecked = i == index;
    }
}
