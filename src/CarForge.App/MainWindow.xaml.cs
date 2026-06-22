using System.Windows;
using CarForge.App.ViewModels;

namespace CarForge.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
#if CODEWALKER
        // liga o render real do .yft (Fase B). Sem a flag, segue no placeholder.
        Views.Viewer3DControl.Loader = new Rendering.CodeWalkerModelLoader();
#endif
        DataContext = new AppViewModel();
    }
}
