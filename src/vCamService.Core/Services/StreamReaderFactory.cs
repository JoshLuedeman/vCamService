namespace vCamService.Core.Services;

public sealed class StreamReaderFactory : IStreamReaderFactory
{
    public IStreamReader Create() => new StreamReader();
}
