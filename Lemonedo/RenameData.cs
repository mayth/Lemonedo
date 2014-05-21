using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Lemonedo
{
    [PropertyChanged.ImplementPropertyChanged]
    class RenameData
    {
        public string NewName { get; set; }
    }
}
