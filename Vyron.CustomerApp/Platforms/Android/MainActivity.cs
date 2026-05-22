using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;

namespace Vyron.CustomerApp;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation
        | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density,
    ScreenOrientation = ScreenOrientation.Portrait)]
public class MainActivity : MauiAppCompatActivity
{
    /// <summary>
    /// Dismiss the soft keyboard whenever the user taps outside an input field.
    /// MAUI does not do this by default on Android; without this override the
    /// keyboard stays open even after focus leaves an Entry/Editor.
    /// </summary>
    public override bool DispatchTouchEvent(MotionEvent? ev)
    {
        if (ev?.Action == MotionEventActions.Down)
        {
            var focused = CurrentFocus;
            if (focused != null)
            {
                var imm = GetSystemService(Context.InputMethodService) as InputMethodManager;
                imm?.HideSoftInputFromWindow(focused.WindowToken, HideSoftInputFlags.None);
                focused.ClearFocus();
            }
        }
        return base.DispatchTouchEvent(ev);
    }
}
