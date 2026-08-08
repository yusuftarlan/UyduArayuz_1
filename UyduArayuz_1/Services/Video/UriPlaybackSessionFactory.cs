using System;
using UyduArayuz_1.Models.Video;

namespace UyduArayuz_1.Services.Video
{
    public sealed class UriPlaybackSessionFactory
        : IVideoPlaybackSessionFactory
    {
        private readonly IUriPlaybackAdapter _adapter;

        public UriPlaybackSessionFactory(
            IUriPlaybackAdapter adapter)
        {
            ArgumentNullException.ThrowIfNull(adapter);

            _adapter = adapter;
        }

        public bool CanCreate(
            VideoSourceDescriptor source)
        {
            return source is LocalFileSourceDescriptor or
                NetworkStreamSourceDescriptor;
        }

        public IVideoPlaybackSession Create(
            VideoSourceDescriptor source)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (!CanCreate(source))
            {
                throw new NotSupportedException(
                    $"'{source.Kind}' kaynağı URI oynatma fabrikası tarafından desteklenmiyor.");
            }

            return new UriPlaybackSession(
                source,
                _adapter);
        }
    }
}