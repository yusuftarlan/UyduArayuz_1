using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UyduArayuz_1.Models.Video;

namespace UyduArayuz_1.Services.Video
{
    public interface IVideoPlaybackSessionResolver
    {
        IVideoPlaybackSession Resolve(
            VideoSourceDescriptor source);
    }
}
