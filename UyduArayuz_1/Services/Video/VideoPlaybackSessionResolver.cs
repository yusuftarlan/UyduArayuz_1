using System;
using System.Collections.Generic;
using System.Linq;
using UyduArayuz_1.Models.Video;

namespace UyduArayuz_1.Services.Video
{
    public sealed class VideoPlaybackSessionResolver
        : IVideoPlaybackSessionResolver
    {
        private readonly IReadOnlyList<IVideoPlaybackSessionFactory> _factories;

        public VideoPlaybackSessionResolver(
            IEnumerable<IVideoPlaybackSessionFactory> factories)
        {
            ArgumentNullException.ThrowIfNull(factories);

            _factories = factories.ToArray();

            if (_factories.Count == 0)
            {
                throw new ArgumentException(
                    "En az bir video session fabrikası sağlanmalıdır.",
                    nameof(factories));
            }
        }

        public IVideoPlaybackSession Resolve(
            VideoSourceDescriptor source)
        {
            ArgumentNullException.ThrowIfNull(source);

            var matchingFactories = _factories
                .Where(factory => factory.CanCreate(source))
                .Take(2)
                .ToArray();

            return matchingFactories.Length switch
            {
                0 => throw new NotSupportedException(
                    $"'{source.Kind}' türündeki video kaynağı desteklenmiyor."),

                1 => matchingFactories[0].Create(source),

                _ => throw new InvalidOperationException(
                    $"'{source.Kind}' kaynağını birden fazla fabrika destekliyor.")
            };
        }
    }
}