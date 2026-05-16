using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Disputes;

public partial class RaiseDisputePage : ContentPage
{
    public RaiseDisputePage(RaiseDisputeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
