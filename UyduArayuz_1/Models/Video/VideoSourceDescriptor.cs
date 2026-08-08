using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UyduArayuz_1.Models.Video
{
    public abstract record VideoSourceDescriptor( string Id, string DisplayName)
    {
        public abstract VideoSourceKind Kind { get; }
    }
}
