using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using System.Collections.ObjectModel;
using System.ComponentModel;
using MsgPack;

namespace Lemonedo
{
    class KeyValuePairTreeNode : CollectionTreeNode
    {
        public override TreeNodeType Type
        {
            get { return TreeNodeType.KeyValuePair; }
        }

        public TreeNode KeyNode { get; set; }
        public TreeNode ValueNode { get; set; }

        BindingList<byte> _value;
        public IEnumerable<byte> Value
        {
            get { return _value; }
        }

        public override IList<TreeNode> Children
        {
            get
            {
                if (!(ValueNode is CollectionTreeNode))
                    throw new NotSupportedException();
                return ValueNode.Children;
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="TreeNode"/> class from a pair of <see cref="ValueNode"/> and <see cref="Node"/> instance.
        /// </summary>
        /// <param name="key">An instance that represents a key of a new instance of <see cref="TreeNode"/> class.</param>
        /// <param name="value">An instance that represents a value of a new instance of <see cref="TreeNode"/> class.</param>
        public KeyValuePairTreeNode(MessagePackObject keyObj, MessagePackObject valueObj, TreeNode parent)
            : this(TreeNode.CreateFromNode(keyObj, parent), TreeNode.CreateFromNode(valueObj, parent), parent)
        {
        }

        public KeyValuePairTreeNode(TreeNode keyNode, TreeNode valueNode, TreeNode parent)
            : base(parent)
        {
            this.Name = keyNode.ToString() + "(" + valueNode.Type +")";
            this.KeyNode = keyNode;
            this.ValueNode = valueNode;

            if (valueNode is ValueTreeNode)
            {
                var v = (ValueTreeNode)valueNode;
                this._value = new BindingList<byte>(v.Value.ToArray());
            }
        }

        public override void AddChild(TreeNode node)
        {
            var collection = ValueNode as CollectionTreeNode;
            if (collection == null)
                throw new NotSupportedException("This method is supported only for collection nodes.");
            this.Children.Add(node);
        }

        public override bool RemoveChild(TreeNode node)
        {
            var collection = ValueNode as CollectionTreeNode;
            if (collection == null)
                throw new NotSupportedException("This method is supported only for collection nodes.");
            return collection.RemoveChild(node);
        }

        public void InsertChild(int index, TreeNode node)
        {
            Contract.Requires<ArgumentOutOfRangeException>(index >= 0);
            var array = ValueNode as ArrayTreeNode;
            if (array == null)
                throw new NotSupportedException();
            array.Insert(index, node);
        }

        internal override void Pack(MsgPack.Packer packer)
        {
            KeyNode.Pack(packer);
            ValueNode.Pack(packer);
        }

        public override bool Equals(TreeNode other)
        {
            if (!(other is KeyValuePairTreeNode))
                return false;
            var kv = (KeyValuePairTreeNode)other;
            return this.KeyNode.Equals(kv.KeyNode) && this.ValueNode.Equals(kv.ValueNode);
        }
    }
}
