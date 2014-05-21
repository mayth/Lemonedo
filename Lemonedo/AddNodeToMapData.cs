using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace Lemonedo
{
    [PropertyChanged.ImplementPropertyChanged]
    class AddNodeToMapData
    {
        public string Key { get; set; }
        public TreeNodeType NodeType { get; set; }
    }
}
