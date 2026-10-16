using AsistenteIA.ViewModels;

namespace AsistenteIA.Views;

public partial class DetailPage : ContentPage
{
    private readonly DetailViewModel _viewModel;

    public DetailPage(DetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }
}
