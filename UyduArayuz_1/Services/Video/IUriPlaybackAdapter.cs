using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UyduArayuz_1.Services.Video
{
    public interface IUriPlaybackAdapter
    {
        Uri? CurrentSource { get; }

        Exception? LastError { get; }

        event EventHandler? PlaybackEnded;

        event EventHandler? PlaybackFailed;

        Task PlayAsync(
            Uri source,
            CancellationToken cancellationToken = default);

        Task StopAsync(
            CancellationToken cancellationToken = default);
    }
}
