using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UyduArayuz_1.Models.Video
{
    public sealed record UsbCameraSourceDescriptor(
         string Id,
         string DisplayName,
         string DeviceId)
         : VideoSourceDescriptor(Id, DisplayName)
    {
        public override VideoSourceKind Kind =>
            VideoSourceKind.UsbCamera;
    }
}
