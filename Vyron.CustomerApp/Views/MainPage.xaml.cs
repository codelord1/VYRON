using Vyron.CustomerApp;

namespace Vyron.CustomerApp.Views;

public partial class MainPage : ContentPage
{
    private readonly (string Title, string Subtitle)[] _slides =
    {
        ("Book laundry pickup from your phone", "Schedule pickup in under a minute. No more hauling baskets across town."),
        ("Choose trusted laundry stores near you", "Verified stores, real reviews, transparent pricing — picked just for your area."),
        ("Track pickup, washing, ironing & delivery", "Live status updates from your door to the laundry and back."),
        ("Get clean clothes delivered safely", "Sealed bags, careful handling, full refund protection on every order.")
    };

    private int _index;

    public MainPage()
    {
        InitializeComponent();
        RenderSlide();
    }

    private void RenderSlide()
    {
        var slide = _slides[_index];
        TitleLabel.Text = slide.Title;
        SubtitleLabel.Text = slide.Subtitle;
        TruckIcon.IsVisible = _index == 0;
        PinIcon.IsVisible = _index == 1;
        SparkIcon.IsVisible = _index == 2;
        ShirtIcon.IsVisible = _index == 3;
        BackButton.IsVisible = _index > 0;
        NextButton.Text = _index == _slides.Length - 1 ? "Get Started" : "Next";

        var dots = new[] { Dot0, Dot1, Dot2, Dot3 };
        for (var i = 0; i < dots.Length; i++)
        {
            dots[i].WidthRequest = i == _index ? 24 : 5;
            dots[i].Color = i == _index
                ? (Color)Application.Current!.Resources["TealDark"]
                : (Color)Application.Current!.Resources["CardStroke"];
        }
    }

    private async void OnNextClicked(object sender, EventArgs e)
    {
        if (_index < _slides.Length - 1)
        {
            _index++;
            RenderSlide();
            return;
        }

        await CompleteOnboardingAsync();
    }

    private void OnBackClicked(object sender, EventArgs e)
    {
        if (_index == 0) return;
        _index--;
        RenderSlide();
    }

    private async void OnSkipTapped(object sender, TappedEventArgs e)
        => await CompleteOnboardingAsync();

    private static async Task CompleteOnboardingAsync()
    {
        Preferences.Default.Set("Vyron.Customer.HasCompletedOnboarding", true);
        await Shell.Current.GoToAsync(AppRoutes.PickupLocation, animate: true);
    }
}
