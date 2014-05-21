using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lemonedo
{
    abstract class LeafTreeNode : TreeNode
    {
        public override IList<TreeNode> Children
        {
            get { return null; }
        }

        public override abstract TreeNodeType Type { get; }

        protected LeafTreeNode(TreeNode parent) : base(parent) { }
    }
}
