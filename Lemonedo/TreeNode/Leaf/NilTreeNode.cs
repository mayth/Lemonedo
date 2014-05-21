using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using MsgPack;

namespace Lemonedo
{
    class NilTreeNode : LeafTreeNode
    {
        public override TreeNodeType Type
        {
            get { return TreeNodeType.Nil; }
        }

        public NilTreeNode(TreeNode parent) : this(null, parent) { }

        /// <summary>
        /// Creates a new instance of <see cref="NilTreeNode"/> class that represents nil.
        /// </summary>
        /// <param name="node">An instance to be a base of a new instance of <see cref="TreeNode"/> class.</param>
        public NilTreeNode(MessagePackObject? node, TreeNode parent)
            : base(parent)
        {
            this.Name = "Nil";
        }

        internal override void Pack(Packer packer)
        {
            packer.PackNull();
        }

        public override bool Equals(TreeNode other)
        {
            return other is NilTreeNode;
        }
    }
}
