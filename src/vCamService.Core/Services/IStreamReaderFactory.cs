namespace vCamService.Core.Services;

/// <summary>
/// Factory for creating <see cref="IStreamReader"/> instances.
/// Enables testability by decoupling creation from the concrete implementation.
/// </summary>
public interface IStreamReaderFactory
{
    IStreamReader Create();
}

public sealed class StreamReaderFactory : IStreamReaderFactory
{
    public IStreamReader Create() => new StreamReader();
}
