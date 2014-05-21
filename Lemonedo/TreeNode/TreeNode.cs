using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;
using System.ComponentModel;
using MsgPack;

namespace Lemonedo
{
    /// <summary>
    /// Represents a node to represent a tree.
    /// </summary>
    [ContractClass(typeof(TreeNodeContract))]
    [PropertyChanged.ImplementPropertyChanged]
    public abstract class TreeNode : IEquatable<TreeNode>
    {
        /// <summary>
        /// Gets or sets whether this node is selected.
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// Gets or sets whether this node is expanded.
        /// </summary>
        public bool IsExpanded { get; set; }

        /// <summary>
        /// Gets or sets the name of this node.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the children node of this node.
        /// </summary>
        public abstract IList<TreeNode> Children { get; }

        /// <summary>
        /// Gets the type of this node.
        /// </summary>
        public abstract TreeNodeType Type { get; }

        /// <summary>
        /// Gets or sets the parent node.
        /// </summary>
        public TreeNode Parent { get; set; }

        /// <summary>
        /// Gets whether this node can contain other nodes.
        /// </summary>
        public bool IsContainerNode
        {
            get { return this.Type == TreeNodeType.Array || this.Type == TreeNodeType.Map || this.Type == TreeNodeType.KeyValuePair; }
        }

        /// <summary>
        /// Initializes a new empty instance of <see cref="TreeNode"/> class.
        /// </summary>
        protected TreeNode(TreeNode parent)
        {
            this.Parent = parent;
        }

        #region Factory
        public static TreeNode Parse(System.IO.Stream stream)
        {
            return Parse(MsgPack.Unpacking.UnpackObject(stream));
        }

        public static TreeNode Parse(MsgPack.MessagePackObject obj)
        {
            if (obj.IsArray)
                return new ArrayTreeNode(obj, null);
            else if (obj.IsMap)
                return new MapTreeNode(obj, null);
            else if (obj.IsNil)
                return new NilTreeNode(obj, null);
            else
                return new ValueTreeNode(obj, null);
        }

        public static TreeNode CreateFromNode(MessagePackObject obj)
        {
            Contract.Ensures(Contract.Result<TreeNode>() != null);
            return CreateFromNode(obj, null);
        }

        /// <summary>
        /// Creates a new instance of <see cref="TreeNode"/> class from <see cref="Node"/> object.
        /// </summary>
        /// <param name="node">An instance to be a base of a new instance of <see cref="TreeNode"/> class.</param>
        /// <returns>A new instance of <see cref="TreeNode"/> that contains values of <paramref name="node"/>.</returns>
        public static TreeNode CreateFromNode(MessagePackObject obj, TreeNode parent)
        {
            Contract.Ensures(Contract.Result<TreeNode>() != null);

            if (obj.IsArray)
                return new ArrayTreeNode(obj, parent);
            if (obj.IsMap)
                return new MapTreeNode(obj, parent);
            if (obj.IsNil)
                return new NilTreeNode(obj, parent);
            return new ValueTreeNode(obj, parent);
            // throw new ArgumentException("Unknown node type.", "node");
        }

        #endregion

        #region Tree Manipulation
        /// <summary>
        /// Removes this node from the children list of the parent node.
        /// </summary>
        /// <returns>true if this node was successfully removed from the parent; otherwise, false. This method also return false if this node is not found in the parent.</returns>
        public bool RemoveFromParent()
        {
            Contract.Requires<InvalidOperationException>(this.Parent != null && this.Parent is CollectionTreeNode);
            return ((CollectionTreeNode)this.Parent).RemoveChild(this);
        }

        /// <summary>
        /// Inserts a new sibling node before this node.
        /// </summary>
        /// <param name="node">The node to insert.</param>
        public void InsertSiblingBefore(TreeNode node)
        {
            Contract.Requires<InvalidOperationException>(
                this.Parent != null
                && (this.Parent is ArrayTreeNode ||
                   (this.Parent is KeyValuePairTreeNode && ((KeyValuePairTreeNode)this.Parent).ValueNode is ArrayTreeNode)
                   ));
            Contract.Requires<ArgumentNullException>(node != null);
            var indexOfThis = this.Parent.Children.IndexOf(this);
            if (this.Parent is ArrayTreeNode)
                ((ArrayTreeNode)this.Parent).Insert(indexOfThis, node);
            else if (this.Parent is KeyValuePairTreeNode)
                ((KeyValuePairTreeNode)this.Parent).InsertChild(indexOfThis, node);
        }

        /// <summary>
        /// Inserts a new sibling node after this node.
        /// </summary>
        /// <param name="node">The node to insert.</param>
        public void InsertSiblingAfter(TreeNode node)
        {
            Contract.Requires<InvalidOperationException>(
                this.Parent != null
                && (this.Parent is ArrayTreeNode ||
                   (this.Parent is KeyValuePairTreeNode && ((KeyValuePairTreeNode)this.Parent).ValueNode is ArrayTreeNode)
                   ));
            Contract.Requires<ArgumentNullException>(node != null);
            var indexOfThis = this.Parent.Children.IndexOf(this);
            if (this.Parent is ArrayTreeNode)
                ((ArrayTreeNode)this.Parent).Insert(indexOfThis + 1, node);
            else if (this.Parent is KeyValuePairTreeNode)
                ((KeyValuePairTreeNode)this.Parent).InsertChild(indexOfThis + 1, node);
        }
        #endregion

        /// <summary>
        /// Searches a node with the spcified option.
        /// </summary>
        /// <param name="keyword">A keyword to search.</param>
        /// <param name="type">Search type.</param>
        /// <exception cref="ArgumentNullException"><paramref name="keyword"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="keyword"/> is an empty string, or <paramref name="type"/> is not acceptable value.</exception>
        /// <returns>If the node that matches with <paramref name="keyword"/> is found, the node is returned; otherwise, null will be returned.</returns>
        public IEnumerable<TreeNode> SearchNodes(string keyword, SearchType type)
        {
            Contract.Requires<ArgumentNullException>(keyword != null);
            Contract.Requires<ArgumentException>(keyword != string.Empty);

            // Check the keyword matches this node.
            switch (type)
            {
                case SearchType.Exact:
                    if (Name == keyword)
                        yield return this;
                    break;
                case SearchType.Partial:
                    if (Name.Contains(keyword))
                        yield return this;
                    break;
                default:
                    throw new ArgumentException("Unknown search type (" + type.ToString() + ")", "type");
            }

            // Check the keyword matches the children of this node.
            if (Children != null)
            {
                foreach (var child in Children)
                    foreach(var result in child.SearchNodes(keyword, type))
                        yield return result;
            }

            // No matches found.
            yield break;
        }

        public string GetPath()
        {
            if (Parent == null)
                return Name;
            return Parent.GetPath() + "\\" + Name;
        }

        #region Serialization
        /// <summary>
        /// Packs this node to the specified stream.
        /// </summary>
        /// <param name="stream">A stream to write.</param>
        public void Pack(System.IO.Stream stream)
        {
            Pack(Packer.Create(stream));
        }

        /// <summary>
        /// Packs this node to the specified file.
        /// </summary>
        /// <param name="path">A path to a file to save.</param>
        public void Pack(string path)
        {
            using (var stream = System.IO.File.Open(path, System.IO.FileMode.Create))
                Pack(stream);
        }

        /// <summary>
        /// Packs this node to the spcified <see cref="MsgPack.Packer"/>.
        /// </summary>
        /// <param name="packer">An instance of <see cref="MsgPack.Packer"/> to write.</param>
        abstract internal void Pack(MsgPack.Packer packer);
        #endregion

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.</returns>
        abstract public bool Equals(TreeNode other);

        public override string ToString()
        {
            return this.Name;
        }
    }

    [ContractClassFor(typeof(TreeNode))]
    abstract partial class TreeNodeContract : TreeNode
    {
        TreeNodeContract() : base(null) { }

        internal override void Pack(Packer packer)
        {
            Contract.Requires<ArgumentNullException>(packer != null);
            throw new NotImplementedException();
        }

        public override bool Equals(TreeNode other)
        {
            throw new NotImplementedException();
        }
    }
}
