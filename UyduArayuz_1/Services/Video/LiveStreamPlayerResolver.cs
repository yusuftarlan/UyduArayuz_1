namespace UyduArayuz_1.Services.Video;

/// <summary>
/// Player seçimini View'den ayıran resolver uygulamasıdır.
/// </summary>
public sealed class LiveStreamPlayerResolver : ILiveStreamPlayerResolver
{
    private readonly IReadOnlyList<ILiveStreamPlayerFactory> _factories;

    public LiveStreamPlayerResolver(IEnumerable<ILiveStreamPlayerFactory> factories)
    {
        ArgumentNullException.ThrowIfNull(factories);
        _factories = factories.ToArray();

        if (_factories.Count == 0)
        {
            throw new ArgumentException(
                "En az bir canlı yayın factory'si kaydedilmelidir.",
                nameof(factories));
        }
    }

    public ILiveStreamPlayer Resolve(LiveStreamProtocol protocol)
    {
        ILiveStreamPlayerFactory[] matches = _factories
            .Where(factory => factory.Protocol == protocol)
            .Take(2)
            .ToArray();

        return matches.Length switch
        {
            0 => throw new NotSupportedException(
                $"'{protocol}' canlı yayın protokolü uygulanmadı."),
            1 => matches[0].Create(),
            _ => throw new InvalidOperationException(
                $"'{protocol}' protokolü için birden fazla factory kaydedildi.")
        };
    }
}
