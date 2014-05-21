using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using Be.Windows.Forms;
using System.IO;
using System.Diagnostics.Contracts;
using System.ComponentModel;
using System.Windows.Forms.Integration;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace Lemonedo
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        class NodeSearchStatus : IDisposable
        {
            IEnumerable<TreeNode> searchNodeResult;
            IEnumerator<TreeNode> searchNodeResultEnumerator;

            public bool IsNodeFound
            {
                get { return searchNodeResult != null && searchNodeResult.Any(); }
            }

            public NodeSearchStatus(TreeNode node, string keyword, SearchType type)
            {
                Contract.Requires<ArgumentNullException>(node != null && keyword != null);
                Contract.Requires<ArgumentException>(keyword != string.Empty && Enum.IsDefined(typeof(SearchType), type));

                searchNodeResult = node.SearchNodes(keyword, type);
                searchNodeResultEnumerator = searchNodeResult.GetEnumerator();
            }

            public TreeNode GetNext()
            {
                if (searchNodeResultEnumerator.MoveNext())
                    return searchNodeResultEnumerator.Current;
                return null;
            }

            public void Reset()
            {
                searchNodeResultEnumerator.Dispose();
                searchNodeResultEnumerator = searchNodeResult.GetEnumerator();
            }

            public void Dispose()
            {
                searchNodeResultEnumerator.Dispose();
            }
        }

        public static readonly RoutedUICommand ExitCommand = new RoutedUICommand("Exit application", "ExitCommand", typeof(MainWindow));
        public static readonly RoutedUICommand SearchNextCommand = new RoutedUICommand("Search the next match.", "SearchCommand", typeof(MainWindow));
        public static readonly RoutedUICommand CancelCommand = new RoutedUICommand("Cancel the current operation.", "CancelCommand", typeof(MainWindow));
        public static readonly RoutedUICommand CopyPathCommand = new RoutedUICommand("Copy the path to the current node.", "CopyPathCommand", typeof(MainWindow));
        public static readonly RoutedUICommand OpenNewTabCommand = new RoutedUICommand("Open the node in a new tab.", "OpenNewTabCommand", typeof(MainWindow));
        public static readonly RoutedUICommand CloseTabCommand = new RoutedUICommand("Close the current tab.", "CloseTabCommand", typeof(MainWindow));
        public static readonly RoutedUICommand AddChildCommand = new RoutedUICommand("Add a new node as a child of this node.", "AddChildCommand", typeof(MainWindow));
        public static readonly RoutedUICommand AddBeforeCommand = new RoutedUICommand("Add a new node before this node.", "AddBeforeCommand", typeof(MainWindow));
        public static readonly RoutedUICommand AddAfterCommand = new RoutedUICommand("Add a new node after this node.", "AddAfterCommand", typeof(MainWindow));
        public static readonly RoutedUICommand RenameCommand = new RoutedUICommand("Rename this node.", "RenameCommand", typeof(MainWindow));
        public static readonly RoutedUICommand DeleteNodeCommand = new RoutedUICommand("Delete this node.", "DeleteNodeCommand", typeof(MainWindow));

        public HexBox CurrentHexBox
        {
            get
            {
                var host = (WindowsFormsHost)editViewTab.SelectedContent;
                if (host == null)
                    return null;
                return (HexBox)host.Child;
            }
        }

        TreeNode treeRootNode;
        string openingFile;
        bool _isModified;
        SearchOption searchOption;
        NodeSearchStatus nodeSearchStatus;
        IList<BinaryViewTabItem> binaryViews;
        Random random = new Random();   // for debug

        bool IsFileModified
        {
            get
            {
                return (binaryViews != null && IsAnyOpeningNodesModified) || _isModified;
            }
        }

        bool IsNodeModified
        {
            get
            {
                return CurrentHexBox != null && CurrentHexBox.ByteProvider != null && CurrentHexBox.ByteProvider.HasChanges();
            }
        }

        bool IsAnyOpeningNodesModified
        {
            get
            {
                return binaryViews.Any(x => x.HexBox.ByteProvider != null && x.HexBox.ByteProvider.HasChanges());
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            nodeList.DataContext = null; 

            openingFile = string.Empty;
            _isModified = false;
            
            searchOption = new SearchOption()
            {
                SearchTarget = Lemonedo.SearchTarget.Node,
                SearchAs = Lemonedo.SearchAs.String,
                SearchBase = SearchValueBase.Hexadecimal,
                SearchType = Lemonedo.SearchType.Partial
            };
            searchOption.PropertyChanged +=
                (sender, e) =>
                {
                    if (nodeSearchStatus != null)
                        nodeSearchStatus.Dispose();
                    nodeSearchStatus = null;
                };
            searchPanel.DataContext = searchOption;

            binaryViews = new List<BinaryViewTabItem>();

            ChangeEditBoxState(false);
        }

        #region Event Implementations
        #region Event Handlers
        private void nodeList_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var node = e.NewValue as TreeNode;

            if (node != null && (node is ValueTreeNode || (node is KeyValuePairTreeNode && ((KeyValuePairTreeNode)node).ValueNode is ValueTreeNode)))
            {
                var openedTab = binaryViews.SingleOrDefault(x => x.Node == node);
                if (openedTab != null)
                {
                    editViewTab.SelectedItem = openedTab.TabItem;
                }
                else
                {
                    if (!editViewTab.HasItems)
                    {
                        var addedView = AddBinaryViewTab(node);
                        addedView.TabItem.IsSelected = true;
                    }
                    else
                    {
                        var selectedView = binaryViews.SingleOrDefault(x => x.TabItem == editViewTab.SelectedItem);

                        // confirmation is needed only when the binary view is updated.
                        if (IsNodeModified && MessageBox.Show("Do you want to save the changes to this element?", "This element is changed!", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                            ApplyChangesToView(selectedView);

                        selectedView.UpdateNode(node);
                    }
                }
                ChangeEditBoxState(true);
            }

            if (node != null)
                pathText.Text = node.GetPath();
            else
                pathText.Text = string.Empty;
        }

        private void TreeViewItem_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = sender as TreeViewItem;
            if (item == null)
                return;
            var node = item.Header as TreeNode;
            if (node == null)
                return;
            OpenNewTabCommand.Execute(node, this);
            e.Handled = true;
        }

        private void editViewTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
                return;
            var selectedTab = e.AddedItems[0] as TabItem;
            var selectedNode = binaryViews.Single(x => x.TabItem == selectedTab).Node;
            SelectAndExpand(selectedNode);
        }
        #endregion

        #region Overrides
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            RemoveAllBinaryViewTabs();
            if (nodeSearchStatus != null)
                nodeSearchStatus.Dispose();

            base.OnClosed(e);
        }
        #endregion
        #endregion

        #region MessagePanel Generator
        private UIElement RootNodeTypeSelectConfirmationContent()
        {
            var segoeUI = new FontFamily("Segoe UI");
            var questionText = new TextBlock(new Run("The file or some nodes are modified. Do you want to apply the changes to nodes and to save this file?"))
            {
                Foreground = Brushes.White,
                FontFamily = segoeUI,
                FontSize = 18,
                TextWrapping = TextWrapping.Wrap
            };
            return questionText;
        }

        private void RootNodeTypeSelectConfirmationButtons(IList<Button> buttons)
        {
            var saveButton = new Button() { Content = "Save", Margin = new Thickness(20, 0, 20, 0) };
            saveButton.Click +=
                (clSender, clArgs) =>
                {
                    try
                    {
                        ApplicationCommands.Save.Execute(null, this);

                        RemoveAllBinaryViewTabs();
                        nodeList.DataContext = null;
                        treeRootNode = null;
                        _isModified = false;

                        messageDialogPanel.DataContext = new SelectRootNodeData() { NodeType = TreeNodeType.Array };
                        ShowMessagePanel(RootNodeTypeSelectorContent, RootNodeTypeSelectorButtons);
                    }
                    catch
                    {
                    }
                };
            var continueAnywayButton = new Button() { Content = "Don't Save", Margin = new Thickness(20, 0, 20, 0) };
            continueAnywayButton.Click +=
                (clSender, clArgs) =>
                {
                    RemoveAllBinaryViewTabs();
                    nodeList.DataContext = null;
                    treeRootNode = null;
                    _isModified = false;

                    messageDialogPanel.DataContext = new SelectRootNodeData() { NodeType = TreeNodeType.Array };
                    ShowMessagePanel(RootNodeTypeSelectorContent, RootNodeTypeSelectorButtons);
                };
            var cancelButton = new Button() { Content = "Cancel", Margin = new Thickness(20, 0, 20, 0) };
            cancelButton.Click +=
                (clSender, clArgs) =>
                {
                    CloseMessagePanel();
                };
            buttons.Add(saveButton);
            buttons.Add(continueAnywayButton);
            buttons.Add(cancelButton);
        }

        private UIElement RootNodeTypeSelectorContent()
        {
            var segoeUI = new FontFamily("Segoe UI");
            var contentPanel = new StackPanel();
            var questionText = new TextBlock(new Run("Choose the type of root node.")) { Foreground = Brushes.White, FontFamily = segoeUI, FontSize = 18 };
            var radioButtons = new StackPanel();
            var selections = new[] { TreeNodeType.Array, TreeNodeType.Map };
            foreach (var selection in selections)
            {
                var radio = new RadioButton()
                {
                    Foreground = Brushes.White,
                    FontFamily = segoeUI,
                    FontSize = 16,
                    GroupName = "newRootNodeType",
                    Content = selection.ToString()
                };
                radio.SetBinding(RadioButton.IsCheckedProperty, new Binding("NodeType") { Converter = new EnumBooleanConverter(), ConverterParameter = selection.ToString(), Mode = BindingMode.TwoWay });
                radioButtons.Children.Add(radio);
            }
            contentPanel.Children.Add(questionText);
            contentPanel.Children.Add(radioButtons);
            return contentPanel;

        }

        private void RootNodeTypeSelectorButtons(IList<Button> buttons)
        {
            var createButton = new Button() { Content = "Create", Margin = new Thickness(20, 0, 20, 0) };
            createButton.Click +=
                (clSender, clArgs) =>
                {
                    var d = messageDialogPanel.DataContext as SelectRootNodeData;
                    if (d == null)
                        throw new InvalidOperationException("SelectRootNodeData is expected.");
                    TreeNode node;
                    switch (d.NodeType)
                    {
                        case TreeNodeType.Array:
                            node = new ArrayTreeNode(null);
                            break;
                        case TreeNodeType.Map:
                            node = new MapTreeNode(null);
                            break;
                        default:
                            throw new InvalidOperationException("Unexpected node type is given.");
                    }
                    treeRootNode = node;
                    nodeList.DataContext = treeRootNode;
                    CloseMessagePanel();
                };
            buttons.Add(createButton);
            var cancelButton = new Button() { Content = "Cancel", Margin = new Thickness(20, 0, 20, 0) };
            cancelButton.Click +=
                (clSender, clArgs) =>
                {
                    CloseMessagePanel();
                };
            buttons.Add(cancelButton);
        }

        private UIElement AddingNodeTypeSelectorContent()
        {
            var segoeUI = new FontFamily("Segoe UI");

            var node = GetSelectedNode();
            if (node.Type == TreeNodeType.KeyValuePair)
                node = ((KeyValuePairTreeNode)node).ValueNode;
            else if (node.Type != TreeNodeType.Array && node.Type != TreeNodeType.Map)
                node = node.Parent;

            // Set the data object to pass the handler
            if (node.Type == TreeNodeType.Array)
                messageDialogPanel.DataContext = new AddNodeToArrayData() { NodeType = TreeNodeType.Array };
            else if (node.Type == TreeNodeType.Map)
                messageDialogPanel.DataContext = new AddNodeToMapData() { NodeType = TreeNodeType.Array };

            var contentPanel = new StackPanel();
            var questionText = new TextBlock(new Run("Choose the type of a new node:")) { Foreground = Brushes.White, FontFamily = segoeUI, FontSize = 18 };
            var radioButtons = new StackPanel();
            var selections = new[] { TreeNodeType.Array, TreeNodeType.Map, TreeNodeType.Value, TreeNodeType.Nil };
            foreach (var sel in selections)
            {
                var radio = new RadioButton()
                {
                    Foreground = Brushes.White,
                    FontFamily = segoeUI,
                    FontSize = 16,
                    GroupName = "arrayNodeType",
                    Content = sel.ToString()
                };
                radio.SetBinding(RadioButton.IsCheckedProperty, new Binding("NodeType") { Converter = new EnumBooleanConverter(), ConverterParameter = sel.ToString(), Mode = BindingMode.TwoWay });
                radioButtons.Children.Add(radio);
            }
            if (node.Type == TreeNodeType.Map)
            {
                var keyPanel = new StackPanel() { Orientation = Orientation.Horizontal };
                keyPanel.Children.Add(new TextBlock(new Run("Key")) { Foreground = Brushes.White, FontFamily = segoeUI, FontSize = 16, VerticalAlignment = System.Windows.VerticalAlignment.Center });
                var keyBox = new TextBox() { Width = 120, Margin = new Thickness(5), FontFamily = segoeUI, FontSize = 16 };
                keyBox.SetBinding(TextBox.TextProperty, new Binding("Key"));
                keyPanel.Children.Add(keyBox);
                contentPanel.Children.Add(keyPanel);
            }
            contentPanel.Children.Add(questionText);
            contentPanel.Children.Add(radioButtons);
            return contentPanel;
        }

        private Action<IList<Button>> AddingNodeTypeSelectorButtons(Action<TreeNode, TreeNode> addingMethod)
        {
            return buttons =>
                {
                    var addButton = new Button() { Content = "Add", Margin = new Thickness(20, 0, 20, 0) };
                    addButton.Click +=
                        (clSender, clArgs) =>
                        {
                            var node = GetSelectedNode();
                            TreeNodeType addingType;
                            TreeNodeType selectedType;
                            ValueTreeNode keyNode = null;
                            if (node.Type == TreeNodeType.KeyValuePair)
                                selectedType = ((KeyValuePairTreeNode)node).ValueNode.Type;
                            else if (node.Type != TreeNodeType.Array && node.Type != TreeNodeType.Map)
                                selectedType = node.Parent.Type;
                            else
                                selectedType = node.Type;

                            if (messageDialogPanel.DataContext is AddNodeToArrayData)
                            {
                                var d = messageDialogPanel.DataContext as AddNodeToArrayData;
                                //if (d == null)
                                //    throw new InvalidOperationException("Expects AddNodeToArrayData");
                                addingType = d.NodeType;
                            }
                            else if (messageDialogPanel.DataContext is AddNodeToMapData)
                            {
                                var d = messageDialogPanel.DataContext as AddNodeToMapData;
                                //if (d == null)
                                //    throw new InvalidOperationException("Expects AddNodeToMapData");
                                addingType = d.NodeType;
                                if (string.IsNullOrWhiteSpace(d.Key))
                                {
                                    MessageBox.Show("Key cannot be empty");
                                    return;
                                }
                                keyNode = new ValueTreeNode(d.Key, node);
                            }
                            else
                                throw new InvalidOperationException("Unexpected data");

                            TreeNode newNode;
                            var parentNode = node is CollectionTreeNode ? node : node.Parent;
                            switch (addingType)
                            {
                                case TreeNodeType.Array:
                                    newNode = new ArrayTreeNode(parentNode);
                                    break;
                                case TreeNodeType.Map:
                                    newNode = new MapTreeNode(parentNode);
                                    break;
                                case TreeNodeType.Value:
                                    // todo
                                    newNode = new ValueTreeNode("test child " + random.Next(), parentNode);
                                    break;
                                case TreeNodeType.Nil:
                                    newNode = new NilTreeNode(parentNode);
                                    break;
                                default:
                                    throw new InvalidOperationException("Unexpected NodeType");
                            }
                            if (selectedType == TreeNodeType.Map)
                                newNode = new KeyValuePairTreeNode(keyNode, newNode, node is CollectionTreeNode ? node : node.Parent);

                            addingMethod(node, newNode);
//                            ((CollectionTreeNode)node).AddChild(newNode);
                            _isModified = true;
                            CloseMessagePanel();
                        };
                    buttons.Add(addButton);

                    var cancelButton = new Button() { Content = "Cancel", Margin = new Thickness(20, 0, 20, 0) };
                    cancelButton.Click +=
                        (clSender, clArgs) =>
                        {
                            CloseMessagePanel();
                        };
                    buttons.Add(cancelButton);
                };
        }

        private UIElement RenameNodeContent()
        {
            var segoeUI = new FontFamily("Segoe UI");
            var panel = new StackPanel();
            var text = new TextBlock(new Run("Enter a new name for this node:")) { Foreground = Brushes.White, FontSize = 18, FontFamily = segoeUI, VerticalAlignment = System.Windows.VerticalAlignment.Center };
            var box = new TextBox() { Width = 180, Margin = new Thickness(5), FontFamily = segoeUI, FontSize = 16 };
            box.SetBinding(TextBox.TextProperty, new Binding("NewName"));
            panel.Children.Add(text);
            panel.Children.Add(box);
            return panel;
        }

        private void RenameNodeButtons(IList<Button> buttons)
        {
            var okButton = new Button() { Content = "Apply", Margin = new Thickness(20, 0, 20, 0) };
            okButton.Click +=
                (sender, e) =>
                {
                    var currentNode = GetSelectedNode() as KeyValuePairTreeNode;
                    if (currentNode == null)
                        throw new InvalidOperationException("Unsupported node.");
                    var renameInfo = messageDialogPanel.DataContext as RenameData;
                    if (renameInfo == null)
                        throw new InvalidOperationException("RenameData is expected.");
                    var keyNode = (ValueTreeNode)currentNode.KeyNode;
                    keyNode.Value.Clear();
                    foreach (var b in Encoding.Unicode.GetBytes(renameInfo.NewName))
                        keyNode.Value.Add(b);
                    keyNode.Name = renameInfo.NewName;
                    currentNode.Name = renameInfo.NewName;

                    var view = binaryViews.FirstOrDefault(v => v.Node == currentNode);
                    if (view != null)
                    {
                        view.Name = renameInfo.NewName;
                    }
                    CloseMessagePanel();
                };
            var cancelButton = new Button { Content = "Cancel", Margin = new Thickness(20, 0, 20, 0) };
            cancelButton.Click +=
                (sender, e) =>
                {
                    CloseMessagePanel();
                };
            buttons.Add(okButton);
            buttons.Add(cancelButton);
        }
        #endregion

        #region Commands
        #region New
        private void NewCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void NewCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var segoeUI = new FontFamily("Segoe UI");
            if (IsFileModified)
            {
                ShowMessagePanel(RootNodeTypeSelectConfirmationContent, RootNodeTypeSelectConfirmationButtons);
            }
            else
            {
                messageDialogPanel.DataContext = new SelectRootNodeData() { NodeType = TreeNodeType.Array };
                ShowMessagePanel(RootNodeTypeSelectorContent, RootNodeTypeSelectorButtons);
            }
        }
        #endregion

        #region Open
        private void OpenCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void OpenCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Contract.Ensures(!IsFileModified);
            Contract.EnsuresOnThrow<Exception>(IsFileModified == Contract.OldValue<bool>(IsFileModified));

            // The current editing box has any modifications, or isModified is true, the file is modified.
            if (IsFileModified && MessageBox.Show("The file or some nodes are modified. Do you want to apply the changes to node and to save this file?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    ApplicationCommands.Save.Execute(null, this);
                }
                catch
                {
                    // When failed to save, do nothing.
                    return;
                }
            }

            var ofd = new Microsoft.Win32.OpenFileDialog();
            if (ofd.ShowDialog() ?? false)
            {
                try
                {
                    using (var stream = ofd.OpenFile())
                    {
                        treeRootNode = TreeNode.Parse(stream);
                    }
                    if (treeRootNode == null)
                    {
                        MessageBox.Show("Error on parsing the MessagePack.");
                        return;
                    }
                    nodeList.DataContext = treeRootNode;
                    openingFile = ofd.FileName;
                    _isModified = false;
                    RemoveAllBinaryViewTabs();
                    window.Title = "msgpack edit - " + openingFile;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to open!\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion

        #region Save
        private void SaveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (string.IsNullOrEmpty(openingFile) && IsFileModified) || (!string.IsNullOrEmpty(openingFile));
        }

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(openingFile))
                    SaveDialog();
                else
                    Save(openingFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot be saved to " + openingFile + "." + Environment.NewLine + ex.Message, "Failed to save", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }
        #endregion

        #region Save As
        private void SaveAsCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (string.IsNullOrEmpty(openingFile) && IsFileModified) || (!string.IsNullOrEmpty(openingFile));
        }

        private void SaveAsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveDialog();
        }
        #endregion

        #region Search
        private void SearchCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void SearchCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (searchGrid.Visibility != System.Windows.Visibility.Visible)
                OpenSearchPanel();
            else
                SearchNextCommand.Execute(null, this);
        }
        #endregion

        #region SearchNext
        private void SearchNextCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void SearchNextCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (nodeSearchStatus != null)
            {
                SelectNextFoundNode();
                return;
            }

            var keyword = searchKeyBox.Text;
            if (string.IsNullOrEmpty(keyword))
            {
                MessageBox.Show("Search keyword is empty!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                switch (searchOption.SearchTarget)
                {
                    case SearchTarget.Binary:
                        SearchBinary(keyword);
                        break;
                    case SearchTarget.Node:
                        SearchNode(keyword);
                        break;
                    default:
                        throw new InvalidOperationException("Unknown search target (" + searchOption.SearchTarget + ") is set.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Search failure:" + Environment.NewLine + ex.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }
        #endregion

        #region Cancel
        private void CancelCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void CancelCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (searchGrid.Visibility == System.Windows.Visibility.Visible)
                CloseSearchPanel();
            if (messageDialogPanel.Visibility == System.Windows.Visibility.Visible)
                CloseMessagePanel();
        }
        #endregion

        #region CopyPath
        private void CopyPathCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !string.IsNullOrEmpty(pathText.Text);
        }

        private void CopyPathCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Clipboard.SetText(pathText.Text);
        }
        #endregion

        #region Exit
        private void ExitCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void ExitCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (IsFileModified && MessageBox.Show("The file is modified. Do you want to save this?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                SaveDialog();

            this.Close();
        }
        #endregion

        #region OpenNewTab
        private void OpenNewTabCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var node = e.Parameter as ValueTreeNode ?? nodeList.SelectedItem as ValueTreeNode;
            e.CanExecute = node != null;
        }

        private void OpenNewTabCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var node = e.Parameter as TreeNode ?? nodeList.SelectedItem as TreeNode;
            var openedTab = binaryViews.SingleOrDefault(x => x.Node == node);
            if (openedTab != null)
            {
                openedTab.TabItem.IsSelected = true;
                return;
            }
            var addedTab = AddBinaryViewTab(node);
            addedTab.TabItem.IsSelected = true;

            //var tabOpenedBy = (TabOpenedBy)Enum.Parse(typeof(TabOpenedBy), e.Parameter.ToString());
            //switch (tabOpenedBy)
            //{
            //    case TabOpenedBy.RightButton:
            //        addedTab.TabItem.IsSelected = true;
            //        break;
            //    case TabOpenedBy.ContextMenu:
            //        break;
            //    default:
            //        break;
            //}
        }
        #endregion

        #region CloseTab
        private void CloseTabCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = editViewTab.HasItems;
        }

        private void CloseTabCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // When this command is executed from a keyboard shortcut, e.Source is set to the instance of MainWindow(this).
            var closingTab = e.Source as TabItem ?? (e.Source == this ? editViewTab.SelectedItem as TabItem : null);
            var closingView = binaryViews.Single(x => x.TabItem == closingTab);
            if (IsNodeModified)
            {
                var dialogResult = MessageBox.Show("Do you want to save the changes to this element?", "This element is changed!", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                switch (dialogResult)
                {
                    case MessageBoxResult.Yes:
                        ApplyChangesToView(closingView);
                        break;
                    case MessageBoxResult.No:
                        break;
                    case MessageBoxResult.Cancel:
                        return;
                }
            }
            RemoveBinaryViewTab(closingView);
        }
        #endregion

        #region AddChild
        private void AddChildCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = false;
            if (nodeList == null)
                return;

            var node = GetSelectedNode();

            if (node != null)
                e.CanExecute = node is CollectionTreeNode;
        }

        private void AddChildCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ShowMessagePanel(AddingNodeTypeSelectorContent, AddingNodeTypeSelectorButtons((target, node) => ((CollectionTreeNode)target).AddChild(node)));
        }
        #endregion

        #region AddBefore
        private void AddBeforeCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanInsertSibling();
        }

        private void AddBeforeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ShowMessagePanel(AddingNodeTypeSelectorContent, AddingNodeTypeSelectorButtons((target, node) => target.InsertSiblingBefore(node)));
            //// stub
            //var node = GetSelectedNode();
            //var newNode = TreeNode.CreateFromNode(Lemonedo.MessagePack.Node.CreateValueNode(Encoding.Unicode.GetBytes("node"), typeof(string)), node.Parent);
            //if (node.Parent is ArrayTreeNode || (node.Parent is KeyValuePairTreeNode && ((KeyValuePairTreeNode)node.Parent).ValueNode is ArrayTreeNode))
            //    node.InsertSiblingBefore(newNode);
            _isModified = true;
        }
        #endregion

        #region AddAfter
        private void AddAfterCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanInsertSibling();
        }

        private void AddAfterCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ShowMessagePanel(AddingNodeTypeSelectorContent, AddingNodeTypeSelectorButtons((target, node) => target.InsertSiblingAfter(node)));
            // stub
            //var node = GetSelectedNode();
            //var newNode = TreeNode.CreateFromNode(Lemonedo.MessagePack.Node.CreateValueNode(Encoding.Unicode.GetBytes("test sibling a" + random.Next()), typeof(string)), node.Parent);
            //node.InsertSiblingAfter(newNode);
            _isModified = true;
        }
        #endregion

        #region Rename
        private void RenameCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var selected = GetSelectedNode();
            e.CanExecute = selected != null && selected.Type == TreeNodeType.KeyValuePair;
        }

        private void RenameCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            messageDialogPanel.DataContext = new RenameData();
            ShowMessagePanel(RenameNodeContent, RenameNodeButtons);
        }
        #endregion

        #region DeleteNode
        private void DeleteNodeCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = false;
            if (nodeList == null)
                return;
            var node = nodeList.SelectedItem as TreeNode;
            if (node != null)
                e.CanExecute = node.Parent != null;
        }

        private void DeleteNodeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var node = GetSelectedNode();
            if (node is CollectionTreeNode && node.Children.Any())
            {
                var segoeUI = new FontFamily("Segoe UI");
                ShowMessagePanel(
                #region Generate content
                    () =>
                    {
                        var content = new Grid();
                        var questionText = new TextBlock(new Run("This node contains some nodes. All children node are deleted if you delete this node. Are you sure to continue?"))
                        {
                            Foreground = Brushes.White,
                            FontFamily = segoeUI,
                            FontWeight = FontWeights.Light,
                            FontSize = 18,
                            TextWrapping = TextWrapping.Wrap
                        };
                        content.Children.Add(questionText);
                        return content;
                    },
                #endregion

                #region Generate buttons
                    buttons =>
                        {
                            var yesButton = new Button() { Content = "Yes", FontFamily = segoeUI, FontWeight = FontWeights.Light, FontSize = 16, Margin = new Thickness(20, 0, 20, 0) };
                            yesButton.Click +=
                                (clSender, clArgs) =>
                                {
                                    if (node.RemoveFromParent())
                                        _isModified = true;
                                    else
                                        MessageBox.Show("Failed");
                                    CloseMessagePanel();
                                };
                            buttons.Add(yesButton);

                            var noButton = new Button() { Content = "No", FontFamily = segoeUI, FontWeight = FontWeights.Light, FontSize = 16, Margin = new Thickness(20, 0, 20, 0) };
                            noButton.Click +=
                                (clSender, clArgs) =>
                                {
                                    CloseMessagePanel();
                                };
                            buttons.Add(noButton);
                        }
                #endregion
                );

            }
            else
                if (node.RemoveFromParent())
                    _isModified = true;
                else
                    MessageBox.Show("Failed");

        }
        #endregion
        #endregion

        #region Private Utility Methods
        #region Tab Control
        private BinaryViewTabItem AddBinaryViewTab(TreeNode node)
        {
            Contract.Requires<ArgumentNullException>(node != null);
            var item = new BinaryViewTabItem(node);
            binaryViews.Add(item);
            editViewTab.Items.Add(item.TabItem);
            return item;
        }

        private void RemoveBinaryViewTab(BinaryViewTabItem viewToBeRemoved)
        {
            editViewTab.Items.Remove(viewToBeRemoved.TabItem);
            binaryViews.Remove(viewToBeRemoved);
            viewToBeRemoved.Dispose();

            if (!editViewTab.HasItems)
                ChangeEditBoxState(false);
        }

        private void RemoveAllBinaryViewTabs()
        {
            editViewTab.Items.Clear();
            foreach (var tab in binaryViews)
                tab.Dispose();
            binaryViews.Clear();
            ChangeEditBoxState(false);
        }
        #endregion

        private TreeNode GetSelectedNode()
        {
            // TreeViewItem is returned only when the root node is selected.
            // When the other nodes are selected, TreeNode will be returned.
            TreeNode node;
            if (nodeList == null)
                return null;
            var tvi = nodeList.SelectedItem as TreeViewItem;
            if (tvi != null)
                node = treeRootNode;
            else
                node = nodeList.SelectedItem as TreeNode;
            return node;
        }

        /// <summary>
        /// Apply changes in the editing control to the specified node.
        /// </summary>
        /// <param name="node">A node to be applied changes.</param>
        private void ApplyChangesToView(BinaryViewTabItem view)
        {
            Contract.Requires<ArgumentNullException>(view != null);

            var provider = view.HexBox.ByteProvider;
            if (provider == null)
                return;
            provider.ApplyChanges();
            var value = new byte[provider.Length];
            for (var i = 0L; i < provider.Length; i++)
                value[i] = provider.ReadByte(i);
            var nodeValue = ((ValueTreeNode)view.Node).Value;
            nodeValue.Clear();
            foreach (var b in value)
                nodeValue.Add(b);
            _isModified = true;
        }

        private void ApplyChangesToAllNodes()
        {
            foreach (var view in binaryViews)
                ApplyChangesToView(view);
        }

        /// <summary>
        /// Save the current node tree to the specified file.
        /// </summary>
        /// <param name="path">Path to a file to save.</param>
        /// <returns>true if this method is succeeded; otherwise, false.</returns>
        private void Save(string path)
        {
            Contract.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(path));

            ApplyChangesToAllNodes();

            // Create a new temporary file, and get the full path to it.
            var tempFilePath = System.IO.Path.GetTempFileName();
            try
            {
                // Write the current state to the temporary file.
                using (var stream = System.IO.File.Open(tempFilePath, System.IO.FileMode.Open))
                {
                    treeRootNode.Pack(stream);
                }
                // Then overwrite the target file by the temporary file.
                File.Copy(tempFilePath, path, true);
            }
            finally
            {
                // Finally, delete the temporary file.
                File.Delete(tempFilePath);
                _isModified = false;
            }
        }

        /// <summary>
        /// Show the save dialog and write all bytes to the specified file.
        /// </summary>
        /// <returns>true when saving is completed. otherwise, false.</returns>
        /// <remarks>When success to save, isModified flag is set to false.</remarks>
        private void SaveDialog()
        {
            var sfd = new Microsoft.Win32.SaveFileDialog()
            {
                OverwritePrompt = true,
                ValidateNames = true
            };

            if (sfd.ShowDialog(this) ?? false)
            {
                try
                {
                    Save(sfd.FileName);
                    openingFile = sfd.FileName;
                    _isModified = false;
                }
                catch (Exception e)
                {
                    MessageBox.Show("Failed to save!" + Environment.NewLine + e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Change the visibility state for the editing box.
        /// </summary>
        /// <param name="isEditable">Whether the editing box is now able to be editable or not.</param>
        private void ChangeEditBoxState(bool isEditable)
        {
            if (isEditable)
            {
                editBoxHiding.Visibility = System.Windows.Visibility.Hidden;
                //formsHost.Visibility = System.Windows.Visibility.Visible;
                searchBinaryRadio.IsEnabled = true;
            }
            else
            {
                editBoxHiding.Visibility = System.Windows.Visibility.Visible;
                //formsHost.Visibility = System.Windows.Visibility.Hidden;
                searchBinaryRadio.IsEnabled = false;
            }
        }
        
        private static void SelectAndExpand(TreeNode node)
        {
            node.IsSelected = true;
            for (var parent = node.Parent; parent != null; parent = parent.Parent)
                parent.IsExpanded = true;
        }

        private void OpenSearchPanel()
        {
            BeginStoryboard((System.Windows.Media.Animation.Storyboard)Resources["searchOpenStoryboard"]);
        }

        private void CloseSearchPanel()
        {
            BeginStoryboard((System.Windows.Media.Animation.Storyboard)Resources["searchCloseStoryboard"]);
        }

        private void ShowMessagePanel(Func<UIElement> contentGenerator, Action<IList<Button>> buttonGenerator)
        {
            messageDialogMainContent.Children.Clear();
            messageDialogMainContent.Children.Add(contentGenerator());

            messageDialogButtons.Children.Clear();
            var buttons = new List<Button>();
            buttonGenerator(buttons);
            foreach (var el in buttons)
                messageDialogButtons.Children.Add(el);
            messageDialogButtons.Columns = buttons.Count;

            var currentTabItem = binaryViews.FirstOrDefault(item => item.TabItem == (TabItem)editViewTab.SelectedItem);
            if (currentTabItem != null)
                currentTabItem.SetTabContentVisibility(false);
            BeginStoryboard((System.Windows.Media.Animation.Storyboard)Resources["messageDialogShowStoryboard"]);
        }

        private void CloseMessagePanel()
        {
            var currentTabItem = binaryViews.FirstOrDefault(item => item.TabItem == (TabItem)editViewTab.SelectedItem);
            if (currentTabItem != null)
                currentTabItem.SetTabContentVisibility(true);
            BeginStoryboard((System.Windows.Media.Animation.Storyboard)Resources["messageDialogHideStoryboard"]);
        }

        private bool CanInsertSibling()
        {
            if (nodeList == null)
                return false;
            var node = nodeList.SelectedItem as TreeNode;
            return node != null && node.Parent != null && node.Parent.IsContainerNode;
        }

        #region Search
        private void SearchBinary(string keyword)
        {
            FindOptions options;
            string prefix = null;
            string value;

            #region get prefix/value
            var prefixDelim = keyword.IndexOf(':');
            if (prefixDelim > 0 && keyword[prefixDelim - 1] != '\\')
            {
                prefix = new string(keyword.TakeWhile(c => c != ':').ToArray());
                value = new string(keyword.SkipWhile(c => c != ':').ToArray()).Substring(1).Trim();
            }
            else
            {
                if (prefixDelim <= 0)
                {
                    value = keyword;
                }
                else
                {
                    value = keyword.Remove(prefixDelim - 1, 1);
                }
            }
            #endregion

            switch (searchOption.SearchAs)
            {
                case SearchAs.Binary:
                    switch (searchOption.SearchBase)
                    {
                        case SearchValueBase.Decimal:
                            options = SearchBinaryDecimal(prefix, value);
                            break;
                        case SearchValueBase.Hexadecimal:
                            options = SearchBinaryHexadecimal(prefix, value);
                            break;
                        default:
                            throw new FormatException("Unknown search value base (" + searchOption.SearchBase + ") is set.");
                    }
                    break;
                case SearchAs.String:
                    options = SearchBinaryString(prefix, value);
                    break;
                default:
                    throw new InvalidOperationException("Unknown search value dealing mode (" + searchOption.SearchAs + ") is set.");
            }

            if (options == null)
                throw new InvalidOperationException("Find arguments is invalid.");

            var result = CurrentHexBox.Find(options);
            if (result == -1)
                MessageBox.Show("No matches found.");
            else if (result == -2)
                MessageBox.Show("Aborted.");
        }

        private FindOptions SearchBinaryDecimal(string prefix, string value)
        {
            Contract.Requires<ArgumentNullException>(value != null);
            Contract.Requires<ArgumentException>(value != string.Empty);

            var regex = new System.Text.RegularExpressions.Regex(@"-?[0-9]+");
            var match = regex.Match(value);
            if (!match.Success)
                throw new FormatException("Unacceptable format of value.");
            var _value = match.Value;

            var findOption = new FindOptions() { Type = FindType.Hex };

            // sbyte -> short -> int -> long  (signed value)
            // byte -> ushort -> uint -> ulong (unsigned value)
            var _prefix = prefix;
            if (string.IsNullOrWhiteSpace(prefix))
                _prefix = "i";

            switch (_prefix)
            {
                case "sb":
                    sbyte sb;
                    if (!sbyte.TryParse(_value, out sb))
                    {
                        System.Diagnostics.Debug.WriteLine("Parsing as sbyte is failed! try to parse as short.");
                        return SearchBinaryDecimal("s", _value);
                    }
                    findOption.Hex = new[] { Convert.ToByte(sb) };
                    break;
                case "b":
                    byte b;
                    if (!byte.TryParse(_value, out b))
                    {
                        System.Diagnostics.Debug.WriteLine("Parsing as byte is failed! try to parse as ushort.");
                        return SearchBinaryDecimal("us", _value);
                    }
                    findOption.Hex = new[] { b };
                    break;
                case "s":
                    short s;
                    if (!short.TryParse(_value, out s))
                    {
                        System.Diagnostics.Debug.WriteLine("Parsing as short is failed! try to parse as int.");
                        return SearchBinaryDecimal("i", _value);
                    }
                    findOption.Hex = BitConverter.GetBytes(s);
                    break;
                case "us":
                    ushort us;
                    if (!ushort.TryParse(_value, out us))
                    {
                        System.Diagnostics.Debug.WriteLine("Parsing as ushort is failed! try to parse as uint.");
                        return SearchBinaryDecimal("ui", _value);
                    }
                    findOption.Hex = BitConverter.GetBytes(us);
                    break;
                case "i":
                    int i;
                    if (!int.TryParse(_value, out i))
                    {
                        System.Diagnostics.Debug.WriteLine("Parsing as int is failed! try to parse as long.");
                        return SearchBinaryDecimal("l", _value);
                    }
                    findOption.Hex = BitConverter.GetBytes(i);
                    break;
                case "ui":
                    uint ui;
                    if (!uint.TryParse(_value, out ui))
                    {
                        System.Diagnostics.Debug.WriteLine("Parsing as uint is failed! try to parse as ulong.");
                        return SearchBinaryDecimal("ul", _value);
                    }
                    findOption.Hex = BitConverter.GetBytes(ui);
                    break;
                case "l":
                    long l;
                    if (!long.TryParse(_value, out l))
                        throw new ArgumentException("Try to parse the value but failed all.");
                    findOption.Hex = BitConverter.GetBytes(l);
                    break;
                case "ul":
                    ulong ul;
                    if (!ulong.TryParse(_value, out ul))
                        throw new ArgumentException("Try to parse the value but failed all.");
                    findOption.Hex = BitConverter.GetBytes(ul);
                    break;
                default:
                    throw new FormatException("Unknown prefix (" + _prefix + ")");
            }
            return findOption;
        }

        private FindOptions SearchBinaryHexadecimal(string prefix, string value)
        {
            Contract.Requires<ArgumentNullException>(value != null);
            Contract.Requires<ArgumentException>(value != string.Empty);

            var regex = new System.Text.RegularExpressions.Regex(@"([0-9a-fA-F]{2})+");
            if (!regex.IsMatch(value))
                throw new FormatException("Unacceptable format as hex value");

            var findOption = new FindOptions() { Type = FindType.Hex };

            // slice string per 2 characters, then convert them to byte.
            // slice example: "f3c1" -> {"f3", "c1"}
            findOption.Hex = value.Slice(2, x => Convert.ToByte(new string(x.ToArray()), 16)).ToArray();
            return findOption;
        }

        private FindOptions SearchBinaryString(string prefix, string value)
        {
            Contract.Requires<ArgumentNullException>(value != null);
            Contract.Requires<ArgumentException>(value != string.Empty);

            var option = new FindOptions() { Type = FindType.Hex };
            var _prefix = prefix;
            if (string.IsNullOrWhiteSpace(_prefix))
                _prefix = "unicode";
            _prefix = _prefix.ToLowerInvariant();
            Encoding encoding;
            switch(_prefix)
            {
                case "utf-8":
                    encoding = Encoding.UTF8;
                    break;
                case "unicode":
                case "utf-16":
                    encoding = Encoding.Unicode;
                    break;
                case "ascii":
                    encoding = Encoding.ASCII;
                    break;
                default:
                    throw new FormatException("Unknown encoding prefix (" + _prefix + ")");
            }
            option.Hex = encoding.GetBytes(value);
            return option;
        }

        private void SearchNode(string keyword)
        {
            nodeSearchStatus = new NodeSearchStatus(treeRootNode, keyword, searchOption.SearchType);
            SelectNextFoundNode();
        }

        private void SelectNextFoundNode()
        {
            if (!nodeSearchStatus.IsNodeFound)
            {
                MessageBox.Show("No matches found.");
                return;
            }

            var next = nodeSearchStatus.GetNext();
            if (next == null)
            {
                var askResult = MessageBox.Show("No next node." + Environment.NewLine + "Do you want to search from the first node?", "End of Nodes", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                if (askResult != MessageBoxResult.Yes)
                {
                    return;
                }
                nodeSearchStatus.Reset();
                next = nodeSearchStatus.GetNext();
            }

            nodeList.Focus();
            SelectAndExpand(next);
            CloseSearchPanel();
        }
        #endregion
        #endregion

    }
}
