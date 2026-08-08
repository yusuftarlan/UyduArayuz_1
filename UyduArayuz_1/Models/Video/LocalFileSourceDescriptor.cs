using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UyduArayuz_1.Models.Video
{
    public sealed record LocalFileSourceDescriptor(
         string Id,
         string DisplayName,
         string FilePath)
         : VideoSourceDescriptor(Id, DisplayName)
    {
        public override VideoSourceKind Kind =>
            VideoSourceKind.LocalFile;
    }
}
