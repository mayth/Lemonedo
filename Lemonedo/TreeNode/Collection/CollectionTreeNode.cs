using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace Lemonedo
{
    abstract class CollectionTreeNode : TreeNode
    {
        protected CollectionTreeNode(TreeNode parent) : base(parent) { }
        
        protected ObservableCollection<TreeNode> _children;
        public override IList<TreeNode> Children
        {
            get { return _children; }
        }

        public override abstract TreeNodeType Type { get; }

        /// <summary>
        /// Adds a new child node to this node.
        /// </summary>
        /// <param name="node">The node to add.</param>
        public abstract void AddChild(TreeNode node);

        /// <summary>
        /// Removes the specified node from the children of this node.
        /// </summary>
        /// <param name="node">The node to remove.</param>
        /// <returns>true if <paramref name="node"/> was successfully removed from the children; otherwise, false. This method also return false if <paramref name="node"/> is not found in the children.</returns>
        public abstract bool RemoveChild(TreeNode node);
    }
}
