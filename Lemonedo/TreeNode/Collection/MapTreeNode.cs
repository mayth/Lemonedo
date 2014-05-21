using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using System.Collections.ObjectModel;
using MsgPack;

namespace Lemonedo
{
    class MapTreeNode : CollectionTreeNode, IDictionary<TreeNode, TreeNode>
    {
        public override TreeNodeType Type
        {
            get { return TreeNodeType.Map; }
        }

        public MapTreeNode(TreeNode parent) : this(null, parent) { }

        /// <summary>
        /// Creates a new instance of <see cref="TreeNode"/> class from <see cref="MapNode"/> object.
        /// </summary>
        /// <param name="map">An instance to be a base of a new instance of <see cref="TreeNode"/> class.</param>
        public MapTreeNode(MessagePackObject? obj, TreeNode parent)
            : base(parent)
        {
            if (obj.HasValue)
            {
                var tmp = new List<TreeNode>();
                var map = obj.Value.AsDictionary();
                if (map.Any())
                    foreach (var kv in map)
                        tmp.Add(new KeyValuePairTreeNode(kv.Key, kv.Value, this));
                this._children = new ObservableCollection<TreeNode>(tmp);
            }
            else
            {
                this._children = new ObservableCollection<TreeNode>();
            }

            this.Name = "Map (" + this.Children.Count + " item)";
            this._children.CollectionChanged +=
                (sender, e) => this.Name = "Map (" + this.Children.Count + " item)";
        }

        public void Add(TreeNode key, TreeNode value)
        {
            this.Children.Add(new KeyValuePairTreeNode(key, value, this));
        }

        public bool ContainsKey(TreeNode key)
        {
            return this.Children.Cast<KeyValuePairTreeNode>().Any(kv => kv.KeyNode == key);
        }

        public ICollection<TreeNode> Keys
        {
            get { return this.Children.Cast<KeyValuePairTreeNode>().Select(kv => kv.KeyNode).ToArray(); }
        }

        public bool Remove(TreeNode key)
        {
            var targetNode = this.Children.Cast<KeyValuePairTreeNode>().FirstOrDefault(kv => kv.KeyNode == key);
            if (targetNode == null)
                return false;
            return this.Children.Remove(targetNode);
        }

        public bool TryGetValue(TreeNode key, out TreeNode value)
        {
            value = this.Children.Cast<KeyValuePairTreeNode>().FirstOrDefault(kv => kv.KeyNode == key);
            return value != null;
        }

        public ICollection<TreeNode> Values
        {
            get { return this.Children.Cast<KeyValuePairTreeNode>().Select(kv => kv.ValueNode).ToArray(); }
        }

        public TreeNode this[TreeNode key]
        {
            get
            {
                return this.Children.Cast<KeyValuePairTreeNode>().First(kv => kv.KeyNode == key);
            }
            set
            {
                if (this.ContainsKey(key))
                {
                    var node = (KeyValuePairTreeNode)this[key];
                    node.ValueNode = value;
                }
                else
                {
                    this.Children.Add(new KeyValuePairTreeNode(key, value, this));
                }
            }
        }

        public void Add(KeyValuePair<TreeNode, TreeNode> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            this.Children.Clear();
        }

        public bool Contains(KeyValuePair<TreeNode, TreeNode> item)
        {
            return this.Children.Cast<KeyValuePairTreeNode>().Any(kv => kv.KeyNode == item.Key && kv.ValueNode == item.Value);
        }

        public void CopyTo(KeyValuePair<TreeNode, TreeNode>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get { return this.Children.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(KeyValuePair<TreeNode, TreeNode> item)
        {
            var target = this.Children.Cast<KeyValuePairTreeNode>().FirstOrDefault(kv => kv.KeyNode == item.Key && kv.ValueNode == item.Value);
            if (target == null)
                return false;
            return this.Remove(item.Key);
        }

        public IEnumerator<KeyValuePair<TreeNode, TreeNode>> GetEnumerator()
        {
            foreach (var item in this.Children.Cast<KeyValuePairTreeNode>())
                yield return new KeyValuePair<TreeNode, TreeNode>(item.KeyNode, item.ValueNode);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            foreach (var item in this.Children.Cast<KeyValuePairTreeNode>())
                yield return new KeyValuePair<TreeNode, TreeNode>(item.KeyNode, item.ValueNode);
        }

        public override void AddChild(TreeNode node)
        {
            this.Children.Add(node);
        }

        public override bool RemoveChild(TreeNode node)
        {
            return this.Children.Remove(node);
        }

        internal override void Pack(Packer packer)
        {
            packer.PackMapHeader(this.Count);
            foreach (var kv in this)
            {
                kv.Key.Pack(packer);
                kv.Value.Pack(packer);
            }
        }

        public override bool Equals(TreeNode other)
        {
            if (!(other is MapTreeNode))
                return false;
            var map = (MapTreeNode)other;
            return this.SequenceEqual(map);
        }
    }
}
