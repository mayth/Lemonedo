using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Be.Windows.Forms;
using System.ComponentModel;
using System.Windows.Controls;
using System.Diagnostics.Contracts;

namespace Lemonedo
{
    [PropertyChanged.ImplementPropertyChanged]
    class BinaryViewTabItem : IDisposable
    {
        static readonly int DefaultMaxTabNameLength = 20;
        public static int MaxTabNameLength { get; set; }

        private string _name;
        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                TabItem.Header = Name.Length > MaxTabNameLength ? Name.Remove(MaxTabNameLength - 3) + "..." : Name;
            }
        }
        public TreeNode Node { get; set; }
        public HexBox HexBox { get; set; }
        public TabItem TabItem { get; set; }

        #region Contracts
        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(Name != null);
            Contract.Invariant(Node != null);
            Contract.Invariant(HexBox != null);
            Contract.Invariant(TabItem != null);
            Contract.Invariant(MaxTabNameLength > 0);
        }
        #endregion

        static BinaryViewTabItem()
        {
            // Set default values.
            MaxTabNameLength = DefaultMaxTabNameLength;
        }

        private BinaryViewTabItem()
        {
            HexBox = new HexBox();
            HexBox.ColumnInfoVisible = true;
            HexBox.LineInfoVisible = true;      // address view on left side
            HexBox.StringViewVisible = true;    // string view on right side

            TabItem = new TabItem();
            TabItem.Content = new System.Windows.Forms.Integration.WindowsFormsHost() { Child = HexBox };
        }

        public BinaryViewTabItem(TreeNode node)
            : this()
        {
            Contract.Requires<ArgumentNullException>(node != null);

            UpdateNode(node);
        }

        public void UpdateNode(TreeNode node)
        {
            Contract.Requires<ArgumentNullException>(node != null);

            Node = node;
            Name = node.Name;

            var provider = HexBox.ByteProvider as IDisposable;
            if (provider != null)
                provider.Dispose();

            IEnumerable<byte> value;
            if (node is ValueTreeNode)
                value = ((ValueTreeNode)node).Value;
            else if (node is KeyValuePairTreeNode)
                value = ((KeyValuePairTreeNode)node).Value;
            else
                throw new ArgumentException();

            HexBox.ByteProvider = new DynamicByteProvider(value.ToArray());
        }

        public void SetTabContentVisibility(bool isVisible)
        {
            ((System.Windows.Forms.Integration.WindowsFormsHost)TabItem.Content).Visibility = isVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
        }

        public void Dispose()
        {
            if (!HexBox.IsDisposed)
            {
                var provider = HexBox.ByteProvider as IDisposable;
                if (provider != null)
                    provider.Dispose();
                provider = null;

                HexBox.Dispose();
            }
        }
    }
}
