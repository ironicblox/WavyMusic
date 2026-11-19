using CommunityToolkit.Mvvm.Input;
using Wavy.Android.ViewModels;
using Wavy.Core.Models;

namespace Wavy.Android.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeAndroidViewModel _vm;

    public HomePage(HomeAndroidViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Refresh();
    }
}
