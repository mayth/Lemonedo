using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using System.ComponentModel;
using MsgPack;

namespace Lemonedo
{
    class ValueTreeNode : LeafTreeNode
    {
        Type UnderlyingType { get; set; }

        public override TreeNodeType Type
        {
            get { return TreeNodeType.Value; }
        }

        BindingList<byte> _value;
        /// <summary>
        /// Gets or sets the value that is contained in this node.
        /// </summary>
        public IList<byte> Value
        {
            get { return _value; }
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ValueTreeNode"/> class that contains the specified value.
        /// </summary>
        /// <param name="noe">An instance to be a base of a new instance of <see cref="ValueTreeNode"/> class.</param>
        public ValueTreeNode(MessagePackObject obj, TreeNode parent)
            : base(parent)
        {
            if (obj.IsRaw)
            {
                var array = obj.AsBinary();
                this.Name = obj.AsString(Encoding.Unicode);
                if (string.IsNullOrWhiteSpace(this.Name))
                {
                    if (array.Any())
                        this.Name = BitConverter.ToString(array.Take(5).ToArray()) + "(" + obj.UnderlyingType == null ? "Unknown" : obj.UnderlyingType.Name + ")";
                    else
                        this.Name = "<Empty Raw-bytes>";
                }
                this._value = new BindingList<byte>(array);
            }
            else
            {
                if (obj.IsTypeOf<Byte>() ?? false) 
                {
                    var v = obj.AsByte();
                    this._value = new BindingList<byte>(new[] { v });
                }
                else if (obj.IsTypeOf<SByte>() ?? false)
                {
                    var v = obj.AsSByte();
                    this._value = new BindingList<byte>(new[] { (byte)v });
                }
                else if (obj.IsTypeOf<UInt16>() ?? false)
                {
                    var v = obj.AsUInt16();
                    this._value = new BindingList<byte>(BitConverter.GetBytes(v));
                }
                else if (obj.IsTypeOf<UInt32>() ?? false)
                {
                    var v = obj.AsUInt32();
                    this._value = new BindingList<byte>(BitConverter.GetBytes(v));
                }
                else if (obj.IsTypeOf<UInt64>() ?? false)
                {
                    var v = obj.AsUInt64();
                    this._value = new BindingList<byte>(BitConverter.GetBytes(v));
                }
                else if (obj.IsTypeOf<Int16>() ?? false)
                {
                    var v = obj.AsInt16();
                    this._value = new BindingList<byte>(BitConverter.GetBytes(v));
                }
                else if (obj.IsTypeOf<Int32>() ?? false)
                {
                    var v = obj.AsInt32();
                    this._value = new BindingList<byte>(BitConverter.GetBytes(v));
                }
                else if (obj.IsTypeOf<Int64>() ?? false)
                {
                    var v = obj.AsInt64();
                    this._value = new BindingList<byte>(BitConverter.GetBytes(v));
                }
                else if (obj.IsTypeOf<Single>() ?? false)
                {
                    var v = obj.AsSingle();
                    this._value = new BindingList<byte>(BitConverter.GetBytes(v));
                }
                else if (obj.IsTypeOf<Double>() ?? false)
                {
                    var v = obj.AsDouble();
                    this._value = new BindingList<byte>(BitConverter.GetBytes(v));
                }
                else if (obj.IsTypeOf<Boolean>() ?? false)
                {
                    var v = obj.AsBoolean();
                    this._value = new BindingList<byte>(BitConverter.GetBytes(v));
                }
                this.Name = "val: " + obj.ToString() + " (" + obj.UnderlyingType.Name + ")";
                this.UnderlyingType = obj.UnderlyingType;
            }
            this._value.ListChanged += OnValueChanged;
        }

        public ValueTreeNode(string value, TreeNode parent)
            : base(parent)
        {
            Contract.Requires<ArgumentNullException>(value != null);
            this.Name = value;
            this._value = new BindingList<byte>(Encoding.Unicode.GetBytes(value));
            this._value.ListChanged += OnValueChanged;
            this.UnderlyingType = typeof(string);
        }

        public ValueTreeNode(IList<byte> array, Type underlyingType, TreeNode parent)
            : base(parent)
        {
            Contract.Requires<ArgumentNullException>(array != null);
            this.Name = BitConverter.ToString(array.Take(5).ToArray()) + "(" + underlyingType == null ? "Unknown" : underlyingType.Name + ")";
            this._value = new BindingList<byte>(array);
            this._value.ListChanged += OnValueChanged;
            this.UnderlyingType = underlyingType;
        }

        private void OnValueChanged(object sender, ListChangedEventArgs e)
        {
            if (this.UnderlyingType == typeof(string))
                this.Name = Encoding.Unicode.GetString(this.Value.ToArray());
            else
                this.Name = BitConverter.ToString(this.Value.Take(5).ToArray()) + "(" + this.UnderlyingType == null ? "UnknownType" : this.UnderlyingType.Name + ")";
        }

        internal override void Pack(Packer packer)
        {
            packer.PackRawHeader(this._value.Count);
            packer.PackRawBody(this._value);
        }

        public override bool Equals(TreeNode other)
        {
            if (!(other is ValueTreeNode))
                return false;
            return ((ValueTreeNode)other).Value.SequenceEqual(this._value);
        }
    }
}
