using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Lemonedo
{
    [PropertyChanged.ImplementPropertyChanged]
    class SelectRootNodeData
    {
        public TreeNodeType NodeType { get; set; }
    }
}
