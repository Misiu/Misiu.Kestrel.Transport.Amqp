using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Misiu.Kestrel.Transport.Amqp;

/// <summary>
/// Kestrel asks factories implementing IConnectionListenerFactory to bind endpoints.
/// We return our AmqpConnectionListener for AmqpEndPoint.
/// </summary>
public sealed class AmqpConnectionListenerFactory : IConnectionListenerFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IOptionsMonitor<AmqpTransportOptions> _optionsMonitor;

    /// <summary>
    /// Initializes a new instance of the <see cref="AmqpConnectionListenerFactory"/> class
    /// </summary>
    public AmqpConnectionListenerFactory(
        ILoggerFactory loggerFactory,
        IOptionsMonitor<AmqpTransportOptions> optionsMonitor)
    {
        _loggerFactory = loggerFactory;
        _optionsMonitor = optionsMonitor;
    }

    /// <inheritdoc />
    public async ValueTask<IConnectionListener> BindAsync(
        EndPoint endpoint,
        CancellationToken cancellationToken = default)
    {
        if (endpoint is not AmqpEndPoint amqpEndPoint)
        {
            throw new NotSupportedException(
                $"Endpoint type '{endpoint.GetType().Name}' is not supported by AmqpConnectionListenerFactory.");
        }

        var optionsName = amqpEndPoint.OptionsName ?? Options.DefaultName;
        var options = _optionsMonitor.Get(optionsName);
        var listener = new AmqpConnectionListener(
            amqpEndPoint,
            options,
            _loggerFactory.CreateLogger<AmqpConnectionListener>());

        await listener.StartAsync(cancellationToken).ConfigureAwait(false);
        return listener;
    }
}
