using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace Lemonedo
{
    public enum SearchTarget
    {
        Unknown,
        Binary,
        Node
    }

    public enum SearchAs
    {
        Unknown,
        String,
        Binary
    }

    public enum SearchValueBase
    {
        Unknown,
        Hexadecimal,
        Decimal
    }

    public enum SearchType
    {
        Unknown,
        Exact,
        Partial
    }

    [PropertyChanged.ImplementPropertyChanged]
    public class SearchOption
    {
        public event PropertyChangedEventHandler PropertyChanged;

        SearchTarget _searchTarget;
        public SearchTarget SearchTarget
        {
            get { return _searchTarget; }
            set
            {
                _searchTarget = value;
                switch (SearchTarget)
                {
                    case Lemonedo.SearchTarget.Binary:
                        SearchAs = Lemonedo.SearchAs.Binary;
                        break;
                    case Lemonedo.SearchTarget.Node:
                        SearchAs = Lemonedo.SearchAs.String;
                        SearchType = Lemonedo.SearchType.Partial;
                        break;
                }
            }
        }
        
        SearchAs _searchAs;
        public SearchAs SearchAs
        {
            get { return _searchAs; }
            set
            {
                _searchAs = value;
                switch (SearchAs)
                {
                    case Lemonedo.SearchAs.Binary:
                        SearchBase = SearchValueBase.Hexadecimal;
                        break;
                    case Lemonedo.SearchAs.String:
                        SearchBase = SearchValueBase.Unknown;
                        break;
                }
            }
        }
        
        public SearchValueBase SearchBase { get; set; }
        public SearchType SearchType { get; set; }
    }
}
