using UyduArayuz_1.Models.Video;

namespace UyduArayuz_1.Services.Video
{
    public interface IVideoPlaybackSessionFactory
    {
        bool CanCreate(VideoSourceDescriptor source);

        IVideoPlaybackSession Create(
            VideoSourceDescriptor source);
    }
}
