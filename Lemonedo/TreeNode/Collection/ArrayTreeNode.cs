using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using MsgPack;

namespace Lemonedo
{
    class ArrayTreeNode : CollectionTreeNode, IList<TreeNode>
    {
        public override TreeNodeType Type
        {
            get { return TreeNodeType.Array; }
        }

        public ArrayTreeNode(TreeNode parent) : this(null, parent) { }

        /// <summary>
        /// Creates a new instance of <see cref="ArrayTreeNode"/> class from <see cref="ArrayNode"/> object.
        /// </summary>
        /// <param name="node">An instance to be a base of a new instance of <see cref="TreeNode"/> class.</param>
        public ArrayTreeNode(MessagePackObject? obj, TreeNode parent)
            : base(parent)
        {
            if (obj.HasValue)
            {
                List<TreeNode> tmp = new List<TreeNode>();
                var array = obj.Value.AsEnumerable();
                if (array.Any())
                    foreach (var item in array)
                        tmp.Add(TreeNode.CreateFromNode(item, this));
                this._children = new ObservableCollection<TreeNode>(tmp);
            }
            else
            {
                this._children = new ObservableCollection<TreeNode>();
            }

            this.Name = "Array (" + this.Children.Count + " item)";
            this._children.CollectionChanged +=
                (sender, e) => this.Name = "Array (" + this.Children.Count + " item)";
        }

        internal override void Pack(Packer packer)
        {
            packer.PackArrayHeader(this._children.Count);
            foreach (var item in this)
                item.Pack(packer);
        }

        public override bool Equals(TreeNode other)
        {
            if (!(other is ArrayTreeNode))
                return false;
            return ((ArrayTreeNode)other)._children.SequenceEqual(this._children);
        }

        public int IndexOf(TreeNode item)
        {
            return this._children.IndexOf(item);
        }

        public void Insert(int index, TreeNode item)
        {
            this._children.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            this._children.RemoveAt(index);
        }

        public TreeNode this[int index]
        {
            get
            {
                return this._children[index];
            }
            set
            {
                this._children[index] = value;
            }
        }

        public void Add(TreeNode item)
        {
            this._children.Add(item);
        }

        public void Clear()
        {
            this._children.Clear();
        }

        public bool Contains(TreeNode item)
        {
            return this._children.Contains(item);
        }

        public void CopyTo(TreeNode[] array, int arrayIndex)
        {
            this._children.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return this._children.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(TreeNode item)
        {
            return this._children.Remove(item);
        }

        public IEnumerator<TreeNode> GetEnumerator()
        {
            return this._children.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return ((System.Collections.IEnumerable)this._children).GetEnumerator();
        }

        public override void AddChild(TreeNode node)
        {
            this.Children.Add(node);
        }

        public override bool RemoveChild(TreeNode node)
        {
            return this.Children.Remove(node);
        }
    }
}
