using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UyduArayuz_1.Models.Video;

namespace UyduArayuz_1.Services.Video
{
    public interface IVideoPlaybackSession : IAsyncDisposable
    {
        VideoSourceDescriptor Source { get; }

        VideoPlaybackState State { get; }

        string? ErrorMessage { get; }

        event EventHandler? StateChanged;

        Task StartAsync(
            CancellationToken cancellationToken = default);

        Task StopAsync(
            CancellationToken cancellationToken = default);
    }
}
