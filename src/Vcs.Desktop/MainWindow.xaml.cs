using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using Vcs.Desktop.Services;
using Vcs.Desktop.ViewModels;

namespace Vcs.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainViewModel(new MockDataService(), Close);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.SidebarWidth))
            {
                AnimateSidebarWidth(viewModel.SidebarWidth);
            }
        };

        DataContext = viewModel;
        SidebarBorder.Width = viewModel.SidebarWidth;
    }

    private void SidebarEdge_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.DragSidebar(e.HorizontalChange);
        }
    }

    private void SidebarEdge_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ResetSidebarDrag();
        }
    }

    private void AnimateSidebarWidth(double targetWidth)
    {
        var animation = new DoubleAnimation
        {
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        SidebarBorder.BeginAnimation(WidthProperty, animation);
    }
}
