using Microsoft.Maui.Devices;

namespace Vyron.CustomerApp.Services;

public static class TapFeedback
{
    public static void HapticClick()
    {
        try
        {
            if (HapticFeedback.Default.IsSupported)
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch
        {
            // Haptic feedback is a nice-to-have and must never block navigation.
        }
    }
}
