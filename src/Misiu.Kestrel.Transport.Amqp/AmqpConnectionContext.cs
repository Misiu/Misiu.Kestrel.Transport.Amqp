using System.IO.Pipelines;
using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Misiu.Kestrel.Transport.Amqp;

/// <summary>
/// One synthetic "connection" per AMQP message. When Kestrel completes handling,
/// we drain Transport.Output (raw HTTP response), publish it back, and ack the delivery.
/// </summary>
public sealed class AmqpConnectionContext : ConnectionContext
{
    private readonly IModel _channel;
    private readonly ulong _deliveryTag;
    private readonly Func<ReadOnlyMemory<byte>, Task> _publishResponse;
    private readonly ILogger _logger;
    private readonly string _id;
    private readonly PipeReader _outputReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="AmqpConnectionContext"/> class
    /// </summary>
    public AmqpConnectionContext(
        IDuplexPipe transport,
        PipeReader outputReader,
        IModel channel,
        ulong deliveryTag,
        Func<ReadOnlyMemory<byte>, Task> publishResponse,
        ILogger logger,
        string id)
    {
        _channel = channel;
        _deliveryTag = deliveryTag;
        _publishResponse = publishResponse;
        _logger = logger;
        _id = id;
        _outputReader = outputReader;

        ConnectionId = id;
        Features = new FeatureCollection();
        Items = new Dictionary<object, object?>(ReferenceEqualityComparer.Instance);
        Transport = transport;
    }

    /// <inheritdoc />
    public override string ConnectionId { get; set; }

    /// <inheritdoc />
    public override IFeatureCollection Features { get; }

    /// <inheritdoc />
    public override IDictionary<object, object?> Items { get; set; }

    /// <inheritdoc />
    public override IDuplexPipe Transport { get; set; }

    /// <inheritdoc />
    public override EndPoint? LocalEndPoint { get; set; }

    /// <inheritdoc />
    public override EndPoint? RemoteEndPoint { get; set; }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        try
        {
            // Complete the output writer to signal we're done
            Transport.Output.Complete();
            
            // Drain full raw HTTP response written by Kestrel into the output reader
            // Use a timeout to prevent hanging if the pipe is never completed
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var ms = new MemoryStream();
            
            var drainTask = Task.Run(async () =>
            {
                while (true)
                {
                    var result = await _outputReader.ReadAsync().ConfigureAwait(false);
                    var buffer = result.Buffer;
                    foreach (var segment in buffer)
                    {
                        await ms.WriteAsync(segment).ConfigureAwait(false);
                    }
                    _outputReader.AdvanceTo(buffer.End);
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            });
            
            await drainTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);

            await _publishResponse(ms.ToArray()).ConfigureAwait(false);
            try
            {
                _channel.BasicAck(_deliveryTag, multiple: false);
            }
            catch
            {
                // Ignore ack errors
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Timeout while draining output pipe for {ConnectionId}, continuing with disposal", _id);
            try
            {
                _channel.BasicAck(_deliveryTag, multiple: false);
            }
            catch
            {
                // Ignore ack errors
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish AMQP response for {ConnectionId}", _id);
            try
            {
                _channel.BasicAck(_deliveryTag, multiple: false);
            }
            catch
            {
                // Ignore ack errors
            }
        }

        await base.DisposeAsync();
    }
}
