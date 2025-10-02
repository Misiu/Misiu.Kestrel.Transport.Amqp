using System.IO.Pipelines;

namespace Misiu.Kestrel.Transport.Amqp;

/// <summary>
/// Simple duplex pipe implementation
/// </summary>
internal sealed class DuplexPipe : IDuplexPipe
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DuplexPipe"/> class
    /// </summary>
    public DuplexPipe(PipeReader input, PipeWriter output)
    {
        Input = input;
        Output = output;
    }

    /// <inheritdoc />
    public PipeReader Input { get; }

    /// <inheritdoc />
    public PipeWriter Output { get; }
}
