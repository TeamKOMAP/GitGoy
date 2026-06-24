using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Vcs.Desktop.Views;

public partial class ProjectView : UserControl
{
    private const double MinCommitEditorHeight = 124;
    private const double SplitterHeight = 6;

    public ProjectView()
    {
        InitializeComponent();
    }

    private void CommitsList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (FindVisualChild<ScrollViewer>(CommitsList) is not { } scrollViewer)
        {
            return;
        }

        scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private void CommitSplitter_DragDelta(object sender, DragDeltaEventArgs e)
    {
        UpdateResizeBounds();
        ClampCommitEditorHeight();
    }

    private void LeftWorkspaceGrid_SizeChanged(object sender, RoutedEventArgs e)
    {
        UpdateResizeBounds();
        ClampCommitEditorHeight();
    }

    private void UpdateResizeBounds()
    {
        var maxChangesHeight = LeftWorkspaceGrid.ActualHeight - SplitterHeight - MinCommitEditorHeight;
        ChangesRow.MaxHeight = Math.Max(ChangesRow.MinHeight, maxChangesHeight);
        CommitEditorRow.MinHeight = MinCommitEditorHeight;
    }

    private void ClampCommitEditorHeight()
    {
        if (CommitEditorRow.ActualHeight >= MinCommitEditorHeight
            && ChangesRow.ActualHeight <= ChangesRow.MaxHeight)
        {
            return;
        }

        ChangesRow.Height = new GridLength(Math.Min(ChangesRow.ActualHeight, ChangesRow.MaxHeight));
        CommitEditorRow.Height = new GridLength(MinCommitEditorHeight);
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}
