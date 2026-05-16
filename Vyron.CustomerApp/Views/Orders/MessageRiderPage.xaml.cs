using Vyron.CustomerApp.ViewModels;

namespace Vyron.CustomerApp.Views.Orders;

public partial class MessageRiderPage : ContentPage
{
    public MessageRiderPage(MessageRiderViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
