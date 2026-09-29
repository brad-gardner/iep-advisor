using System.Net;
using System.Net.Sockets;

namespace IepAssistant.Api.Extensions;

/// <summary>
/// <see cref="SocketsHttpHandler.ConnectCallback"/> that turns on TCP keepalive. A non-streaming
/// Claude call sends nothing while Claude writes the whole answer, and Azure drops outbound
/// connections idle for ~4 minutes without telling either end; keepalive probes count as traffic.
/// </summary>
public static class KeepAliveConnect
{
    /// <remarks>
    /// Each address gets its own socket. Handing one socket a <see cref="DnsEndPoint"/> lets it try
    /// the addresses in turn, but on Linux a socket cannot connect again after a failed attempt, so
    /// an unreachable first address (App Service resolves IPv6 it cannot route) fails the whole
    /// call with PlatformNotSupportedException.
    /// </remarks>
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var endpoint = context.DnsEndPoint;
        var addresses = await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken);
        // Same preference as the default handler would reach in practice: IPv4 first.
        var ordered = addresses.OrderBy(a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1);

        Exception? lastError = null;
        foreach (var address in ordered)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 60);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 30);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 5);
                await socket.ConnectAsync(new IPEndPoint(address, endpoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                socket.Dispose();
                lastError = ex;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        throw new HttpRequestException(
            $"Could not connect to {endpoint.Host}:{endpoint.Port} on any resolved address.", lastError);
    }
}
