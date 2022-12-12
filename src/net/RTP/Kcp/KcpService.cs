using System;
using System.Buffers;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using KcpSharp;
using Microsoft.Extensions.Logging;
using SIPSorcery.Sys;

namespace SIPSorcery.Net;

internal class KcpService : IUdpService, IKcpTransport, IDisposable
{
    protected static ILogger logger = Log.Logger;
    
    private readonly KcpConversation _conversation;
    private readonly EndPoint _endPoint;
    private readonly int _mtu;
    private readonly IUdpServiceDispatcher _sender;
    private CancellationTokenSource? _cts;
    
    public delegate void KcpDataReceivedDelegate(EndPoint endPoint, byte[] data);
    public event KcpDataReceivedDelegate? OnDataReceived;

    public KcpService(
        IUdpServiceDispatcher sender, 
        EndPoint endPoint,
        KcpConversationOptions options,
        uint conversationId)
    {
        _sender = sender;
        _endPoint = endPoint;
        _conversation = new KcpConversation(this, (int)conversationId, options);
        _mtu = options.Mtu;
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => ReceiveLoop(_cts));
        logger.LogDebug("{Now}: Connected from {EndPoint}", DateTime.Now, endPoint);
    }

    public void Dispose()
    {
        logger.LogDebug("{Now}: Connection from {EndPoint} eliminated.", DateTime.Now, _endPoint);
        Interlocked.Exchange(ref _cts, null)?.Dispose();
        _conversation.Dispose();
    }

    ValueTask IKcpTransport.SendPacketAsync(Memory<byte> packet, CancellationToken cancellationToken)
    {
        return _sender.SendPacketAsync(_endPoint, packet, cancellationToken);
    }

    ValueTask IUdpService.InputPacketAsync(ReadOnlyMemory<byte> packet, CancellationToken cancellationToken)
    {
        return _conversation.InputPakcetAsync(packet, cancellationToken);
    }

    void IUdpService.SetTransportClosed()
    {
        _conversation.SetTransportClosed();
    }
    
    private async Task ReceiveLoop(CancellationTokenSource cts)
    {
        var cancellationToken = cts.Token;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await _conversation.WaitToReceiveAsync(cancellationToken);
                if (result.TransportClosed)
                {
                    break;
                }

                var buffer = ArrayPool<byte>.Shared.Rent(result.BytesReceived);
                try
                {
                    if (!_conversation.TryReceive(buffer, out result))
                        // We don't need to check for result.TransportClosed because there is no way TryReceive can return true when transport is closed.
                    {
                        return;
                    }

                    logger.LogDebug("Message received from {EndPoint}. Length = {ResultBytesReceived} bytes.", _endPoint, result.BytesReceived);
                    OnDataReceived?.Invoke(_endPoint, buffer[..result.BytesReceived]);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
        finally
        {
            cts.Dispose();
        }
    }
}
