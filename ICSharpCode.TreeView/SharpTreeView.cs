﻿// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ICSharpCode.TreeView
{
	public class SharpTreeView : ListView
	{
		static SharpTreeView()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(SharpTreeView),
			                                         new FrameworkPropertyMetadata(typeof(SharpTreeView)));

			SelectionModeProperty.OverrideMetadata(typeof(SharpTreeView),
			                                       new FrameworkPropertyMetadata(SelectionMode.Extended));

			AlternationCountProperty.OverrideMetadata(typeof(SharpTreeView),
			                                          new FrameworkPropertyMetadata(2));

            DefaultItemContainerStyleKey =
                new ComponentResourceKey(typeof(SharpTreeView), "DefaultItemContainerStyleKey");

            VirtualizingStackPanel.VirtualizationModeProperty.OverrideMetadata(typeof(SharpTreeView),
			                                                                   new FrameworkPropertyMetadata(VirtualizationMode.Recycling));
			
			RegisterCommands();
		}

		public static ResourceKey DefaultItemContainerStyleKey { get; private set; }

		public SharpTreeView()
		{
			SetResourceReference(ItemContainerStyleProperty, DefaultItemContainerStyleKey);
		}

		public static readonly DependencyProperty RootProperty =
			DependencyProperty.Register("Root", typeof(SharpTreeNode), typeof(SharpTreeView));

		public SharpTreeNode Root
		{
			get { return (SharpTreeNode)GetValue(RootProperty); }
			set { SetValue(RootProperty, value); }
		}

		public static readonly DependencyProperty ShowRootProperty =
			DependencyProperty.Register("ShowRoot", typeof(bool), typeof(SharpTreeView),
			                            new FrameworkPropertyMetadata(true));

		public bool ShowRoot
		{
			get { return (bool)GetValue(ShowRootProperty); }
			set { SetValue(ShowRootProperty, value); }
		}

		public static readonly DependencyProperty ShowRootExpanderProperty =
			DependencyProperty.Register("ShowRootExpander", typeof(bool), typeof(SharpTreeView),
			                            new FrameworkPropertyMetadata(false));

		public bool ShowRootExpander
		{
			get { return (bool)GetValue(ShowRootExpanderProperty); }
			set { SetValue(ShowRootExpanderProperty, value); }
		}

		public static readonly DependencyProperty AllowDropOrderProperty =
			DependencyProperty.Register("AllowDropOrder", typeof(bool), typeof(SharpTreeView));

		public bool AllowDropOrder
		{
			get { return (bool)GetValue(AllowDropOrderProperty); }
			set { SetValue(AllowDropOrderProperty, value); }
		}

		public static readonly DependencyProperty ShowLinesProperty =
			DependencyProperty.Register("ShowLines", typeof(bool), typeof(SharpTreeView),
			                            new FrameworkPropertyMetadata(true));

		public bool ShowLines
		{
			get { return (bool)GetValue(ShowLinesProperty); }
			set { SetValue(ShowLinesProperty, value); }
		}

		public static bool GetShowAlternation(DependencyObject obj)
		{
			return (bool)obj.GetValue(ShowAlternationProperty);
		}

		public static void SetShowAlternation(DependencyObject obj, bool value)
		{
			obj.SetValue(ShowAlternationProperty, value);
		}

		public static readonly DependencyProperty ShowAlternationProperty =
			DependencyProperty.RegisterAttached("ShowAlternation", typeof(bool), typeof(SharpTreeView),
			                                    new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
		
		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property == RootProperty ||
			    e.Property == ShowRootProperty ||
			    e.Property == ShowRootExpanderProperty) {
				Reload();
			}
		}

        private TreeFlattener _flattener;

        private void Reload()
		{
			if (_flattener != null) {
				_flattener.Stop();
			}
			if (Root != null) {
				if (!(ShowRoot && ShowRootExpander)) {
					Root.IsExpanded = true;
				}
				_flattener = new TreeFlattener(Root, ShowRoot);
				_flattener.CollectionChanged += flattener_CollectionChanged;
				this.ItemsSource = _flattener;
			}
		}

        private void flattener_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			// Deselect nodes that are being hidden, if any remain in the tree
			if (e.Action == NotifyCollectionChangedAction.Remove && Items.Count > 0) {
				List<SharpTreeNode> selectedOldItems = null;
				foreach (SharpTreeNode node in e.OldItems) {
					if (node.IsSelected) {
						if (selectedOldItems == null)
							selectedOldItems = new List<SharpTreeNode>();
						selectedOldItems.Add(node);
					}
				}
				if (selectedOldItems != null) {
					var list = SelectedItems.Cast<SharpTreeNode>().Except(selectedOldItems).ToList();
					SetSelectedItems(list);
					if (SelectedItem == null && this.IsKeyboardFocusWithin) {
						// if we removed all selected nodes, then move the focus to the node
						// preceding the first of the old selected nodes
						SelectedIndex = Math.Max(0, e.OldStartingIndex - 1);
						if (SelectedIndex >= 0)
							FocusNode((SharpTreeNode)SelectedItem);
					}
				}
			}
		}
		
		protected override DependencyObject GetContainerForItemOverride()
		{
			return new SharpTreeViewItem();
		}

		protected override bool IsItemItsOwnContainerOverride(object item)
		{
			return item is SharpTreeViewItem;
		}

		protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
		{
			base.PrepareContainerForItemOverride(element, item);
			var container = element as SharpTreeViewItem;
			container.ParentTreeView = this;
			// Make sure that the line renderer takes into account the new bound data
			if (container.NodeView != null && container.NodeView.LinesRenderer != null) {
				container.NodeView.LinesRenderer.InvalidateVisual();
			}
		}

        private bool _doNotScrollOnExpanding;
		
		/// <summary>
		/// Handles the node expanding event in the tree view.
		/// This method gets called only if the node is in the visible region (a SharpTreeNodeView exists).
		/// </summary>
		internal void HandleExpanding(SharpTreeNode node)
		{
			if (_doNotScrollOnExpanding)
				return;
			SharpTreeNode lastVisibleChild = node;
			while (true) {
				SharpTreeNode tmp = lastVisibleChild.Children.LastOrDefault(c => c.IsVisible);
				if (tmp != null) {
					lastVisibleChild = tmp;
				} else {
					break;
				}
			}
			if (lastVisibleChild != node) {
				// Make the the expanded children are visible; but don't scroll down
				// to much (keep node itself visible)
				base.ScrollIntoView(lastVisibleChild);
				// For some reason, this only works properly when delaying it...
				Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(
					delegate {
						base.ScrollIntoView(node);
					}));
			}
		}
		
		protected override void OnKeyDown(KeyEventArgs e)
		{
			SharpTreeViewItem container = e.OriginalSource as SharpTreeViewItem;
			switch (e.Key) 
			{
				case Key.Left:
					if (container != null && ItemsControl.ItemsControlFromItemContainer(container) == this)
                    {
                        if (!ShowRootExpander && container.Node.IsRoot)
                            break;

                        if (container.Node.IsExpanded)
                        {
                            container.Node.IsExpanded = false;
                        }
                        else if (container.Node.Parent != null)
                        {
                            this.FocusNode(container.Node.Parent);
                        }
                        e.Handled = true;
                        break;
                    }
                    break;
				case Key.Right:
					if (container != null && ItemsControl.ItemsControlFromItemContainer(container) == this) 
					{
						if (!container.Node.IsExpanded && container.Node.ShowExpander) 
						{
							container.Node.IsExpanded = true;
						} 
						else if (container.Node.Children.Count > 0) 
						{
							// jump to first child:
							container.MoveFocus(new TraversalRequest(FocusNavigationDirection.Down));
						}
						e.Handled = true;
					}
					break;
				case Key.Return:
					if (container != null && Keyboard.Modifiers == ModifierKeys.None && this.SelectedItems.Count == 1 && this.SelectedItem == container.Node) 
					{
						e.Handled = true;
						container.Node.ActivateItem(e);
					}
					break;
				case Key.Space:
					if (container != null && Keyboard.Modifiers == ModifierKeys.None && this.SelectedItems.Count == 1 && this.SelectedItem == container.Node) 
					{
						e.Handled = true;
						if (container.Node.IsCheckable) 
						{
							if(container.Node.IsChecked == null) // If partially selected, we want to select everything
								container.Node.IsChecked = true;
							else
								container.Node.IsChecked = !container.Node.IsChecked;
						} 
						else 
						{
							container.Node.ActivateItem(e);
						}
					}
					break;
				case Key.Add:
					if (container != null && ItemsControl.ItemsControlFromItemContainer(container) == this) 
					{
						container.Node.IsExpanded = true;
						e.Handled = true;
					}
					break;
				case Key.Subtract:
					if (container != null && ItemsControl.ItemsControlFromItemContainer(container) == this) 
					{
						container.Node.IsExpanded = false;
						e.Handled = true;
					}
					break;
				case Key.Multiply:
					if (container != null && ItemsControl.ItemsControlFromItemContainer(container) == this) 
					{
						container.Node.IsExpanded = true;
						ExpandRecursively(container.Node);
						e.Handled = true;
					}
					break;
			}
			if (!e.Handled)
				base.OnKeyDown(e);
		}
		
		void ExpandRecursively(SharpTreeNode node)
		{
			if (node.CanExpandRecursively) {
				node.IsExpanded = true;
				foreach (SharpTreeNode child in node.Children) {
					ExpandRecursively(child);
				}
			}
		}
		
		/// <summary>
		/// Scrolls the specified node in view and sets keyboard focus on it.
		/// </summary>
		public void FocusNode(SharpTreeNode node)
		{
			if (node == null)
				throw new ArgumentNullException("node");
			ScrollIntoView(node);
			// WPF's ScrollIntoView() uses the same if/dispatcher construct, so we call OnFocusItem() after the item was brought into view.
			if (this.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated) 
			{
				OnFocusItem(node);
			} 
			else 
			{
				this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new DispatcherOperationCallback(this.OnFocusItem), node);
			}
		}
		
		public void ScrollIntoView(SharpTreeNode node)
		{
			if (node == null)
				throw new ArgumentNullException("node");
			_doNotScrollOnExpanding = true;
			foreach (SharpTreeNode ancestor in node.Ancestors())
				ancestor.IsExpanded = true;
			_doNotScrollOnExpanding = false;
			base.ScrollIntoView(node);
		}

        private object OnFocusItem(object item)
		{
			var element = this.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
			if (element != null) {
				element.Focus();
			}
			return null;
		}

		#region Track selection
		
		protected override void OnSelectionChanged(SelectionChangedEventArgs e)
		{
			foreach (SharpTreeNode node in e.RemovedItems) {
				node.IsSelected = false;
			}
			foreach (SharpTreeNode node in e.AddedItems) {
				node.IsSelected = true;
			}
			base.OnSelectionChanged(e);
		}

        #endregion

        #region Drag and Drop
        protected override void OnDragEnter(DragEventArgs e)
        {
            OnDragOver(e);
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;

            if (Root != null /*&& !ShowRoot*/)
            {
                e.Handled = true;
                e.Effects = Root.GetDropEffect(e, Root.Children.Count);
            }
        }

        protected override void OnDrop(DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;

            if (Root != null /*&& !ShowRoot*/)
            {
                e.Handled = true;
                e.Effects = Root.GetDropEffect(e, Root.Children.Count);
                if (e.Effects != DragDropEffects.None)
                    Root.InternalDrop(e, Root.Children.Count);
            }
        }

        internal void HandleDragEnter(SharpTreeViewItem item, DragEventArgs e)
        {
            HandleDragOver(item, e);
        }

        internal void HandleDragOver(SharpTreeViewItem item, DragEventArgs e)
        {
            HidePreview();
            e.Effects = DragDropEffects.None;

            var target = GetDropTarget(item, e);
            if (target != null)
            {
                e.Handled = true;
                e.Effects = target.Effect;
                ShowPreview(target.Item, target.Place);
            }
        }

        internal void HandleDrop(SharpTreeViewItem item, DragEventArgs e)
        {
            try
            {
                HidePreview();

                var target = GetDropTarget(item, e);
                if (target != null)
                {
                    e.Handled = true;
                    e.Effects = target.Effect;
                    target.Node.InternalDrop(e, target.Index);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                throw;
            }
        }

        internal void HandleDragLeave(SharpTreeViewItem item, DragEventArgs e)
        {
            HidePreview();
            e.Handled = true;
        }

        private class DropTarget
		{
			public SharpTreeViewItem Item;
			public DropPlace Place;
			public double Y;
			public SharpTreeNode Node;
			public int Index;
			public DragDropEffects Effect;
		}

        private DropTarget GetDropTarget(SharpTreeViewItem item, DragEventArgs e)
		{
			var dropTargets = BuildDropTargets(item, e);
			var y = e.GetPosition(item).Y;
            return dropTargets.FirstOrDefault(target => target.Y >= y);
        }

        private List<DropTarget> BuildDropTargets(SharpTreeViewItem item, DragEventArgs e)
		{
			var result = new List<DropTarget>();
			var node = item.Node;
			var h = item.ActualHeight;
			var y1 = 0.2 * h;
			var y2 = h / 2;
			var y3 = h - y1;
			var posY = e.GetPosition(item).Y;
			if (posY > y1 && posY < y3)
            {
				TryAddDropTarget(result, item, DropPlace.Inside, e);
			}
			else if (AllowDropOrder)
            {
                if (posY <= y1)
                {
					TryAddDropTarget(result, item, DropPlace.Before, e);
				}
                else
				{
					if (node.IsExpanded && node.Children.Count > 0)
					{
						var firstChildItem = ItemContainerGenerator.ContainerFromItem(node.Children[0]) as SharpTreeViewItem;
						TryAddDropTarget(result, firstChildItem, DropPlace.Before, e);
					}
					else
					{
						TryAddDropTarget(result, item, DropPlace.After, e);
					}
				}
			}

			if (result.Count == 2) 
			{
				if (result[0].Place == DropPlace.Inside && result[1].Place != DropPlace.Inside) 
				{
					result[0].Y = y3;
				}
				else if (result[0].Place != DropPlace.Inside && result[1].Place == DropPlace.Inside) 
				{
					result[0].Y = y1;
				}
				else 
				{
					result[0].Y = y2;
				}
			}
			else if (result.Count == 3) 
			{
				result[0].Y = y1;
				result[1].Y = y3;
			}
			if (result.Count > 0) 
			{
				result[result.Count - 1].Y = h;
			}
			return result;
		}

        private void TryAddDropTarget(List<DropTarget> targets, SharpTreeViewItem item, DropPlace place, DragEventArgs e)
		{
			GetNodeAndIndex(item, place, out var targetNode, out var index);

			if (targetNode != null)
			{
				var dragItems = SelectedItems.OfType<SharpTreeNode>();
				var effect = CheckOnCurrentItem(dragItems, targetNode, AllowDropOrder) ? DragDropEffects.None : targetNode.GetDropEffect(e, index);
				if (effect == DragDropEffects.Move && SelectedItem is SharpTreeNode node && node.Parent == targetNode)
				{
					var currentIndex = item.Node.Parent.Children.IndexOf(node);
					if (index == currentIndex || index == currentIndex + 1)
						return;
				}
				if (effect != DragDropEffects.None)
				{
					targets.Add(new DropTarget()
					{
						Item = item,
						Place = place,
						Node = targetNode,
						Index = index,
						Effect = effect
					});
				}
			}
		}

        private static bool CheckOnCurrentItem(IEnumerable<SharpTreeNode> dragItems, SharpTreeNode targetNode, bool allowDropOrder)
        {
            return dragItems.Any(dragItem => 
								 dragItem == targetNode || // Попытка добавить/переместить узел в самого себя
								 (!allowDropOrder && dragItem.Parent == targetNode) || //Попытка добавить/переместить в текущего родителя, т.е. в туже самую папку
								 CheckInParent(targetNode, dragItem)); //Попытка добавить/переместить родительский узел в дочерний
        }

        private static bool CheckInParent(SharpTreeNode targetNode, SharpTreeNode dragItem)
        {
			if (targetNode.Parent == null)
				return false;

            bool parentToChild;
            if (targetNode.Parent == dragItem)
                return true;
            else
                parentToChild = CheckInParent(targetNode.Parent, dragItem);
            return parentToChild;
        }

        private void GetNodeAndIndex(SharpTreeViewItem item, DropPlace place, out SharpTreeNode node, out int index)
		{
			node = null;
			index = 0;

			if (place == DropPlace.Inside) 
			{
				node = item.Node;
				index = node.Children.Count;
			}
			else if (place == DropPlace.Before)
			{
				if (item.Node.Parent != null)
				{
					node = item.Node.Parent;
					index = node.Children.IndexOf(item.Node);
				}
			}
			else
			{
				if (item.Node.Parent != null)
				{
					node = item.Node.Parent;
					index = node.Children.IndexOf(item.Node) + 1;
				}
			}
		}

		private SharpTreeViewItem _previewTreeViewItem;
		private InsertMarker _insertMarker;
        private DropPlace _previewPlace;

        private enum DropPlace
		{
			Before, Inside, After
		}
		public Func<Brush> GetPreviewInsideTextBackground = () => SystemColors.HighlightBrush;
		public Func<Brush> GetPreviewInsideForeground = () => SystemColors.HighlightTextBrush;

        private void ShowPreview(SharpTreeViewItem item, DropPlace place)
		{
			_previewTreeViewItem = item;
			_previewPlace = place;
			var previewNodeView = item.NodeView;

			if (place == DropPlace.Inside)
			{
				//previewNodeView.TextBackground = GetPreviewInsideTextBackground();
				//previewNodeView.Foreground = GetPreviewInsideForeground();
				_previewTreeViewItem.Background = GetPreviewInsideTextBackground();
				_previewTreeViewItem.Foreground = GetPreviewInsideForeground();
            }
			else
			{
				if (_insertMarker == null)
				{
					var adornerLayer = AdornerLayer.GetAdornerLayer(this);
					var adorner = new GeneralAdorner(this);
					_insertMarker = new InsertMarker();
					adorner.Child = _insertMarker;
					adornerLayer.Add(adorner);
				}
				_insertMarker.Visibility = Visibility.Visible;

				var p1 = previewNodeView.TransformToVisual(this).Transform(new Point());
				var p = new Point(p1.X + previewNodeView.CalculateIndent(item.Node) + 4.5, p1.Y - 3 - item.Padding.Top);

				if (place == DropPlace.After)
					p.Y += previewNodeView.ActualHeight + item.Padding.Top + item.Padding.Bottom;

				_insertMarker.Margin = new Thickness(p.X, p.Y, 0, 0);
				_insertMarker.Width = item.ActualWidth - p.X;
			}
		}

        private void HidePreview()
		{
			if (_previewTreeViewItem != null)
			{
				_previewTreeViewItem.ClearValue(BackgroundProperty);
				_previewTreeViewItem.ClearValue(ForegroundProperty);

				if (_insertMarker != null) 
				{
					_insertMarker.Visibility = Visibility.Collapsed;
				}
				_previewTreeViewItem = null;
			}
		}
		#endregion
		
		#region Cut / Copy / Paste / Delete Commands

        private static void RegisterCommands()
		{
			CommandManager.RegisterClassCommandBinding(typeof(SharpTreeView),
			                                           new CommandBinding(ApplicationCommands.Cut, HandleExecuted_Cut, HandleCanExecute_Cut));

			CommandManager.RegisterClassCommandBinding(typeof(SharpTreeView),
			                                           new CommandBinding(ApplicationCommands.Copy, HandleExecuted_Copy, HandleCanExecute_Copy));

			CommandManager.RegisterClassCommandBinding(typeof(SharpTreeView),
			                                           new CommandBinding(ApplicationCommands.Paste, HandleExecuted_Paste, HandleCanExecute_Paste));

			CommandManager.RegisterClassCommandBinding(typeof(SharpTreeView),
			                                           new CommandBinding(ApplicationCommands.Delete, HandleExecuted_Delete, HandleCanExecute_Delete));
		}

        private static void HandleExecuted_Cut(object sender, ExecutedRoutedEventArgs e)
		{
			e.Handled = true;
			var treeView = (SharpTreeView)sender;
			var nodes = treeView.GetTopLevelSelection().ToArray();
			if (nodes.Length > 0)
				nodes[0].Cut(nodes);
		}

        private static void HandleCanExecute_Cut(object sender, CanExecuteRoutedEventArgs e)
		{
			var treeView = (SharpTreeView)sender;
			var nodes = treeView.GetTopLevelSelection().ToArray();
			e.CanExecute = nodes.Length > 0 && nodes[0].CanCut(nodes);
			e.Handled = true;
		}

        private static void HandleExecuted_Copy(object sender, ExecutedRoutedEventArgs e)
		{
			e.Handled = true;
			var treeView = (SharpTreeView)sender;
			var nodes = treeView.GetTopLevelSelection().ToArray();
			if (nodes.Length > 0)
				nodes[0].Copy(nodes);
		}

        private static void HandleCanExecute_Copy(object sender, CanExecuteRoutedEventArgs e)
		{
			var treeView = (SharpTreeView)sender;
			var nodes = treeView.GetTopLevelSelection().ToArray();
			e.CanExecute = nodes.Length > 0 && nodes[0].CanCopy(nodes);
			e.Handled = true;
		}

        private static void HandleExecuted_Paste(object sender, ExecutedRoutedEventArgs e)
		{
			var treeView = (SharpTreeView)sender;
			var data = Clipboard.GetDataObject();
			if (data != null) {
				var selectedNode = (treeView.SelectedItem as SharpTreeNode) ?? treeView.Root;
				if (selectedNode != null)
					selectedNode.Paste(data);
			}
			e.Handled = true;
		}

        private static void HandleCanExecute_Paste(object sender, CanExecuteRoutedEventArgs e)
		{
			var treeView = (SharpTreeView)sender;
			var data = Clipboard.GetDataObject();
			if (data == null) {
				e.CanExecute = false;
			} else {
				var selectedNode = (treeView.SelectedItem as SharpTreeNode) ?? treeView.Root;
				e.CanExecute = selectedNode != null && selectedNode.CanPaste(data);
			}
			e.Handled = true;
		}

        private static void HandleExecuted_Delete(object sender, ExecutedRoutedEventArgs e)
		{
			e.Handled = true;
			var treeView = (SharpTreeView)sender;
			var nodes = treeView.GetTopLevelSelection().ToArray();
			if (nodes.Length > 0)
				nodes[0].Delete(nodes);
		}

        private static void HandleCanExecute_Delete(object sender, CanExecuteRoutedEventArgs e)
		{
			var treeView = (SharpTreeView)sender;
			var nodes = treeView.GetTopLevelSelection().ToArray();
			e.CanExecute = nodes.Length > 0 && nodes[0].CanDelete(nodes);
			e.Handled = true;
		}
		
		/// <summary>
		/// Gets the selected items which do not have any of their ancestors selected.
		/// </summary>
		public IEnumerable<SharpTreeNode> GetTopLevelSelection()
		{
			var selection = this.SelectedItems.OfType<SharpTreeNode>();
			var selectionHash = new HashSet<SharpTreeNode>(selection);
			return selection.Where(item => item.Ancestors().All(a => !selectionHash.Contains(a)));
		}

		#endregion
	}
}
