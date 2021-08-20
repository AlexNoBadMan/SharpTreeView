// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace ICSharpCode.TreeView
{
	/// <summary>
	/// Collection that validates that inserted nodes do not have another parent.
	/// </summary>
	public sealed class SharpTreeNodeCollection : IList<SharpTreeNode>, INotifyCollectionChanged
	{
        private readonly SharpTreeNode _parent;
        private List<SharpTreeNode> _list = new List<SharpTreeNode>();
        private bool _isRaisingEvent;
		
		public SharpTreeNodeCollection(SharpTreeNode parent)
		{
			_parent = parent;
		}
		
		public event NotifyCollectionChangedEventHandler CollectionChanged;

        private void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
		{
			Debug.Assert(!_isRaisingEvent);
			_isRaisingEvent = true;
			try {
				_parent.OnChildrenChanged(e);
				if (CollectionChanged != null)
					CollectionChanged(this, e);
			} finally {
				_isRaisingEvent = false;
			}
		}

        private void ThrowOnReentrancy()
		{
			if (_isRaisingEvent)
				throw new InvalidOperationException();
		}

        private void ThrowIfValueIsNullOrHasParent(SharpTreeNode node)
		{
			if (node == null)
				throw new ArgumentNullException("node");
			if (node.ModelParent != null)
				throw new ArgumentException("The node already has a parent", "node");
		}
		
		public SharpTreeNode this[int index] {
			get {
				return _list[index];
			}
			set {
				ThrowOnReentrancy();
				var oldItem = _list[index];
				if (oldItem == value)
					return;
				ThrowIfValueIsNullOrHasParent(value);
				_list[index] = value;
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, oldItem, index));
			}
		}
		
		public int Count {
			get { return _list.Count; }
		}
		
		bool ICollection<SharpTreeNode>.IsReadOnly {
			get { return false; }
		}
		
		public int IndexOf(SharpTreeNode node)
		{
			if (node == null || node.ModelParent != _parent)
				return -1;
			else
				return _list.IndexOf(node);
		}
		
		public void Insert(int index, SharpTreeNode node)
		{
			ThrowOnReentrancy();
			ThrowIfValueIsNullOrHasParent(node);
			_list.Insert(index, node);
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, node, index));
		}
		
		public void InsertRange(int index, IEnumerable<SharpTreeNode> nodes)
		{
			if (nodes == null)
				throw new ArgumentNullException("nodes");
			ThrowOnReentrancy();
			List<SharpTreeNode> newNodes = nodes.ToList();
			if (newNodes.Count == 0)
				return;
			foreach (SharpTreeNode node in newNodes) {
				ThrowIfValueIsNullOrHasParent(node);
			}
			_list.InsertRange(index, newNodes);
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newNodes, index));
		}
		
		public void RemoveAt(int index)
		{
			ThrowOnReentrancy();
			var oldItem = _list[index];
			_list.RemoveAt(index);
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, oldItem, index));
		}
		
		public void RemoveRange(int index, int count)
		{
			ThrowOnReentrancy();
			if (count == 0)
				return;
			var oldItems = _list.GetRange(index, count);
			_list.RemoveRange(index, count);
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, oldItems, index));
		}
		
		public void Add(SharpTreeNode node)
		{
			ThrowOnReentrancy();
			ThrowIfValueIsNullOrHasParent(node);
			_list.Add(node);
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, node, _list.Count - 1));
		}
		
		public void AddRange(IEnumerable<SharpTreeNode> nodes)
		{
			InsertRange(this.Count, nodes);
		}
		
		public void Clear()
		{
			ThrowOnReentrancy();
			var oldList = _list;
			_list = new List<SharpTreeNode>();
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, oldList, 0));
		}
		
		public bool Contains(SharpTreeNode node)
		{
			return IndexOf(node) >= 0;
		}
		
		public void CopyTo(SharpTreeNode[] array, int arrayIndex)
		{
			_list.CopyTo(array, arrayIndex);
		}
		
		public bool Remove(SharpTreeNode item)
		{
			int pos = IndexOf(item);
			if (pos >= 0) {
				RemoveAt(pos);
				return true;
			} else {
				return false;
			}
		}
		
		public IEnumerator<SharpTreeNode> GetEnumerator()
		{
			return _list.GetEnumerator();
		}
		
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return _list.GetEnumerator();
		}
		
		public void RemoveAll(Predicate<SharpTreeNode> match)
		{
			if (match == null)
				throw new ArgumentNullException("match");
			ThrowOnReentrancy();
			int firstToRemove = 0;
			for (int i = 0; i < _list.Count; i++) {
				bool removeNode;
				_isRaisingEvent = true;
				try {
					removeNode = match(_list[i]);
				} finally {
					_isRaisingEvent = false;
				}
				if (!removeNode) {
					if (firstToRemove < i) {
						RemoveRange(firstToRemove, i - firstToRemove);
						i = firstToRemove - 1;
					} else {
						firstToRemove = i + 1;
					}
					Debug.Assert(firstToRemove == i + 1);
				}
			}
			if (firstToRemove < _list.Count) {
				RemoveRange(firstToRemove, _list.Count - firstToRemove);
			}
		}
    }
}
