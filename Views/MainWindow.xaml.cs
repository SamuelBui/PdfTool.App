using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PdfTool.App.Models;
using PdfTool.App.ViewModels;

namespace PdfTool.App.Views;

public partial class MainWindow : Window
{
    private const double AutoScrollEdgeThreshold = 56;
    private const double AutoScrollStep = 8;
    private const string MergeFilesDragDataFormat = "PdfTool.App.MergeFiles";
    private const string MergePagesDragDataFormat = "PdfTool.App.MergePages";
    private const string SplitPagesDragDataFormat = "PdfTool.App.SplitPages";
    private bool _isMergePageSweepSelecting;
    private Point? _mergeFileDragStartPoint;
    private Point? _mergePageDragStartPoint;
    private ListBox? _mergeFileDragListBox;
    private ListBox? _mergePageDragListBox;
    private PdfPageOrganizerItem? _mergePageToggleCandidate;
    private ListBox? _mergePageToggleListBox;
    private bool _mergePageDragTriggered;
    private ListBox? _mergeSweepListBox;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => SyncProtectPasswordBoxes();
    }

    private void ProtectUserPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel && sender is PasswordBox passwordBox)
        {
            mainViewModel.Protect.UserPassword = passwordBox.Password;
        }
    }

    private void ProtectOwnerPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel && sender is PasswordBox passwordBox)
        {
            mainViewModel.Protect.OwnerPassword = passwordBox.Password;
        }
    }

    private void ProtectCommonPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel && sender is PasswordBox passwordBox)
        {
            mainViewModel.Protect.BatchCommonPassword = passwordBox.Password;
        }
    }

    private void ProtectToggleUserPasswordVisibility_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.Protect.ToggleUserPasswordVisibility();
            SyncProtectPasswordBoxes();
        }
    }

    private void ProtectToggleOwnerPasswordVisibility_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.Protect.ToggleOwnerPasswordVisibility();
            SyncProtectPasswordBoxes();
        }
    }

    private void ProtectToggleCommonPasswordVisibility_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.Protect.ToggleCommonPasswordVisibility();
            SyncProtectPasswordBoxes();
        }
    }

    private void ProtectClearAllPasswords_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.Protect.ClearAllPasswords();
            SyncProtectPasswordBoxes();
        }
    }

    private void MergeDropZone_OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void MergeDropZone_OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel mainViewModel)
        {
            return;
        }

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        if (files == null)
        {
            return;
        }

        mainViewModel.Merge.AddFiles(files.Where(path => path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)));
    }

    private void MergeQueueList_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        if (FindAncestor<ListBox>(e.OriginalSource as DependencyObject) is ListBox innerList &&
            !ReferenceEquals(innerList, listBox))
        {
            _mergeFileDragStartPoint = null;
            _mergeFileDragListBox = null;
            return;
        }

        _mergeFileDragStartPoint = e.GetPosition(listBox);
        _mergeFileDragListBox = listBox;
    }

    private void MergeQueueList_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _mergeFileDragStartPoint = null;
        _mergeFileDragListBox = null;
    }

    private void MergeQueueList_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not ListBox listBox || !ReferenceEquals(_mergeFileDragListBox, listBox))
        {
            return;
        }

        if (FindAncestor<ListBox>(e.OriginalSource as DependencyObject) is ListBox innerList &&
            !ReferenceEquals(innerList, listBox))
        {
            return;
        }

        if (_mergeFileDragStartPoint == null || !HasExceededDragThreshold(e.GetPosition(listBox), _mergeFileDragStartPoint.Value))
        {
            return;
        }

        var selectedFiles = listBox.SelectedItems.Cast<PdfFileItem>().ToList();
        if (selectedFiles.Count == 0)
        {
            return;
        }

        var sourceItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (sourceItem?.DataContext is not PdfFileItem sourceFile || !selectedFiles.Contains(sourceFile))
        {
            return;
        }

        var dataObject = new DataObject();
        dataObject.SetData(MergeFilesDragDataFormat, selectedFiles);
        DragDrop.DoDragDrop(listBox, dataObject, DragDropEffects.Move);
        _mergeFileDragStartPoint = null;
        _mergeFileDragListBox = null;
    }

    private void MergeQueueList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.Merge.OpenSelectedFileCommand.Execute(null);
        }
    }

    private void MergeRotateLeftSelectedPages_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel mainViewModel)
        {
            return;
        }

        mainViewModel.Merge.RotateSelectedPages(-90);
    }

    private void MergeRotateRightSelectedPages_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel mainViewModel)
        {
            return;
        }

        mainViewModel.Merge.RotateSelectedPages(90);
    }

    private void MergeRemoveSelectedFiles_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel mainViewModel)
        {
            return;
        }

        var selectedFiles = MergeQueueList.SelectedItems.Cast<PdfFileItem>().ToList();
        mainViewModel.Merge.RemoveFiles(selectedFiles);
    }

    private void MergeQueueList_OnDragOver(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel mainViewModel || sender is not ListBox listBox)
        {
            return;
        }

        AutoScroll(listBox, e.GetPosition(listBox), Orientation.Vertical);
        var targetItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as PdfFileItem;
        mainViewModel.Merge.SetFileDropTarget(targetItem);

        if (e.Data.GetDataPresent(MergeFilesDragDataFormat))
        {
            e.Effects = DragDropEffects.Move;
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void MergeQueueList_OnDragLeave(object sender, DragEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.Merge.ClearDropTargets();
        }
    }

    private void MergeQueueList_OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel mainViewModel || sender is not ListBox listBox)
        {
            return;
        }

        mainViewModel.Merge.ClearDropTargets();

        if (e.Data.GetDataPresent(MergeFilesDragDataFormat))
        {
            var draggedItems = e.Data.GetData(MergeFilesDragDataFormat) as List<PdfFileItem>;
            var targetItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as PdfFileItem;

            if (draggedItems != null && draggedItems.Count > 0)
            {
                mainViewModel.Merge.ReorderFiles(draggedItems, targetItem);
                return;
            }
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files != null)
            {
                mainViewModel.Merge.AddFiles(files.Where(path => path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)));
            }
        }
    }

    private void MergePageList_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        _mergePageDragStartPoint = e.GetPosition(listBox);
        _mergePageDragListBox = listBox;
        _mergePageDragTriggered = false;

        var clickedItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == ModifierKeys.None &&
            clickedItem?.DataContext is PdfPageOrganizerItem clickedPage &&
            clickedPage.IsSelected)
        {
            _mergePageToggleCandidate = clickedPage;
            _mergePageToggleListBox = listBox;
            e.Handled = true;
            return;
        }

        _mergePageToggleCandidate = null;
        _mergePageToggleListBox = null;
    }

    private void MergePageList_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_mergePageToggleCandidate != null &&
            ReferenceEquals(_mergePageToggleListBox, sender as ListBox) &&
            !_mergePageDragTriggered)
        {
            _mergePageToggleCandidate.IsSelected = false;
            e.Handled = true;
        }

        _mergePageDragStartPoint = null;
        _mergePageDragListBox = null;
        _mergePageToggleCandidate = null;
        _mergePageToggleListBox = null;
        _mergePageDragTriggered = false;
    }

    private void MergePageList_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        if (_isMergePageSweepSelecting && e.RightButton == MouseButtonState.Pressed && ReferenceEquals(_mergeSweepListBox, listBox))
        {
            SelectMergePageFromPointer(listBox, e.GetPosition(listBox));
            e.Handled = true;
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed || !ReferenceEquals(_mergePageDragListBox, listBox))
        {
            return;
        }

        if (_mergePageDragStartPoint == null || !HasExceededDragThreshold(e.GetPosition(listBox), _mergePageDragStartPoint.Value))
        {
            return;
        }

        var selectedPages = listBox.SelectedItems.Cast<PdfPageOrganizerItem>().ToList();
        if (selectedPages.Count == 0)
        {
            return;
        }

        var sourceItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (sourceItem?.DataContext is not PdfPageOrganizerItem sourcePage || !selectedPages.Contains(sourcePage))
        {
            return;
        }

        var dataObject = new DataObject();
        dataObject.SetData(MergePagesDragDataFormat, selectedPages);
        _mergePageDragTriggered = true;
        DragDrop.DoDragDrop(listBox, dataObject, DragDropEffects.Move);
        _mergePageDragStartPoint = null;
        _mergePageDragListBox = null;
        _mergePageToggleCandidate = null;
        _mergePageToggleListBox = null;
        _mergePageDragTriggered = false;
    }

    private void MergePageList_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        _isMergePageSweepSelecting = true;
        _mergeSweepListBox = listBox;
        listBox.CaptureMouse();

        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == ModifierKeys.None)
        {
            foreach (var page in listBox.Items.OfType<PdfPageOrganizerItem>())
            {
                page.IsSelected = false;
            }
        }

        SelectMergePageFromPointer(listBox, e.GetPosition(listBox));
        e.Handled = true;
    }

    private void MergePageList_OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        if (ReferenceEquals(_mergeSweepListBox, listBox))
        {
            _isMergePageSweepSelecting = false;
            _mergeSweepListBox = null;
            listBox.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void MergePageList_OnDragOver(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel mainViewModel || sender is not ListBox listBox)
        {
            return;
        }

        AutoScroll(listBox, e.GetPosition(listBox), Orientation.Horizontal);
        var parentFile = listBox.DataContext as PdfFileItem;
        var targetPage = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as PdfPageOrganizerItem;
        mainViewModel.Merge.SetPageDropTarget(parentFile, targetPage);

        e.Effects = e.Data.GetDataPresent(MergePagesDragDataFormat) || e.Data.GetDataPresent(MergeFilesDragDataFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;

        e.Handled = true;
    }

    private void MergePageList_OnDragLeave(object sender, DragEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.Merge.ClearDropTargets();
        }
    }

    private void MergePageList_OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel mainViewModel || sender is not ListBox listBox)
        {
            return;
        }

        mainViewModel.Merge.ClearDropTargets();

        if (!e.Data.GetDataPresent(MergePagesDragDataFormat) && !e.Data.GetDataPresent(MergeFilesDragDataFormat))
        {
            return;
        }

        var parentFile = listBox.DataContext as PdfFileItem;
        if (parentFile == null)
        {
            return;
        }

        var targetPage = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as PdfPageOrganizerItem;
        if (e.Data.GetDataPresent(MergePagesDragDataFormat))
        {
            var draggedPages = e.Data.GetData(MergePagesDragDataFormat) as List<PdfPageOrganizerItem>;
            if (draggedPages == null || draggedPages.Count == 0)
            {
                return;
            }

            mainViewModel.Merge.InsertPagesIntoFile(parentFile, draggedPages, targetPage);
            return;
        }

        if (e.Data.GetDataPresent(MergeFilesDragDataFormat))
        {
            var draggedFiles = e.Data.GetData(MergeFilesDragDataFormat) as List<PdfFileItem>;
            if (draggedFiles != null && draggedFiles.Count > 0)
            {
                mainViewModel.Merge.InsertFilesIntoFile(parentFile, draggedFiles, targetPage);
            }
        }
    }

    private void SplitPageList_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not ListBox listBox)
        {
            return;
        }

        var selectedPages = listBox.SelectedItems.Cast<PdfPageOrganizerItem>().ToList();
        if (selectedPages.Count == 0)
        {
            return;
        }

        var sourceItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (sourceItem == null)
        {
            return;
        }

        var dataObject = new DataObject();
        dataObject.SetData(SplitPagesDragDataFormat, selectedPages);
        DragDrop.DoDragDrop(listBox, dataObject, DragDropEffects.Move);
    }

    private void SplitPageList_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(SplitPagesDragDataFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;

        e.Handled = true;
    }

    private void SplitPageList_OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel mainViewModel || sender is not ListBox)
        {
            return;
        }

        if (!e.Data.GetDataPresent(SplitPagesDragDataFormat))
        {
            return;
        }

        var draggedPages = e.Data.GetData(SplitPagesDragDataFormat) as List<PdfPageOrganizerItem>;
        if (draggedPages == null || draggedPages.Count == 0)
        {
            return;
        }

        var targetItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as PdfPageOrganizerItem;
        mainViewModel.Split.ReorderPages(draggedPages, targetItem);
    }

    private static bool HasExceededDragThreshold(Point currentPosition, Point dragStart)
    {
        return Math.Abs(currentPosition.X - dragStart.X) >= SystemParameters.MinimumHorizontalDragDistance ||
               Math.Abs(currentPosition.Y - dragStart.Y) >= SystemParameters.MinimumVerticalDragDistance;
    }

    private static void AutoScroll(ItemsControl itemsControl, Point pointerPosition, Orientation orientation)
    {
        var scrollViewer = FindDescendant<ScrollViewer>(itemsControl);
        if (scrollViewer == null)
        {
            return;
        }

        if (orientation == Orientation.Vertical)
        {
            if (pointerPosition.Y <= AutoScrollEdgeThreshold)
            {
                scrollViewer.ScrollToVerticalOffset(Math.Max(0, scrollViewer.VerticalOffset - AutoScrollStep));
            }
            else if (pointerPosition.Y >= itemsControl.ActualHeight - AutoScrollEdgeThreshold)
            {
                scrollViewer.ScrollToVerticalOffset(Math.Min(scrollViewer.ScrollableHeight, scrollViewer.VerticalOffset + AutoScrollStep));
            }

            return;
        }

        if (pointerPosition.X <= AutoScrollEdgeThreshold)
        {
            scrollViewer.ScrollToHorizontalOffset(Math.Max(0, scrollViewer.HorizontalOffset - AutoScrollStep));
        }
        else if (pointerPosition.X >= itemsControl.ActualWidth - AutoScrollEdgeThreshold)
        {
            scrollViewer.ScrollToHorizontalOffset(Math.Min(scrollViewer.ScrollableWidth, scrollViewer.HorizontalOffset + AutoScrollStep));
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static T? FindDescendant<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root == null)
        {
            return null;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                return match;
            }

            var nestedMatch = FindDescendant<T>(child);
            if (nestedMatch != null)
            {
                return nestedMatch;
            }
        }

        return null;
    }

    private static void SelectMergePageFromPointer(ListBox listBox, Point position)
    {
        var hit = listBox.InputHitTest(position) as DependencyObject;
        var page = FindAncestor<ListBoxItem>(hit)?.DataContext as PdfPageOrganizerItem;
        if (page != null)
        {
            page.IsSelected = true;
        }
    }

    private void SyncProtectPasswordBoxes()
    {
        if (DataContext is not MainViewModel mainViewModel)
        {
            return;
        }

        ProtectUserPasswordBox.Password = mainViewModel.Protect.UserPassword;
        ProtectOwnerPasswordBox.Password = mainViewModel.Protect.OwnerPassword;
        ProtectCommonPasswordBox.Password = mainViewModel.Protect.BatchCommonPassword;
    }
}
