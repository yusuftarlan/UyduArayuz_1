using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UyduArayuz_1.Models.Video
{
    public sealed record NetworkStreamSourceDescriptor(
        string Id,
        string DisplayName,
        Uri StreamUri)
        : VideoSourceDescriptor(Id, DisplayName)
    {
        public override VideoSourceKind Kind =>
            VideoSourceKind.NetworkStream;
    }
}
