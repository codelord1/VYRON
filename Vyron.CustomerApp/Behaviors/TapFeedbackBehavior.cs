using CommunityToolkit.Maui.Behaviors;

namespace Vyron.CustomerApp.Behaviors;

public class TapFeedbackBehavior : TouchBehavior
{
    public TapFeedbackBehavior()
    {
        PressedScale = 0.97;
        PressedOpacity = 0.82;
        PressedAnimationDuration = 70;
        DefaultAnimationDuration = 110;
        ShouldMakeChildrenInputTransparent = true;
    }
}
