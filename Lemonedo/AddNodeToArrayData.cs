using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace Lemonedo
{
    [PropertyChanged.ImplementPropertyChanged]
    class AddNodeToArrayData
    {
        public TreeNodeType NodeType { get; set; }
    }
}
