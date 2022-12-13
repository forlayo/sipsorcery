using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using KcpSharp;
using Microsoft.Extensions.Logging;
using SIPSorcery.Sys;

namespace SIPSorcery.Net
{
    public class KcpUdpReceiver: IUdpReceiver
    {
        protected static ILogger logger = Log.Logger;
        
        public event PacketReceivedDelegate OnPacketReceived;
        public event Action<string> OnClosed;
        
        protected readonly Socket m_socket;
        protected IPEndPoint m_localEndPoint;
        protected AddressFamily m_addressFamily;
        protected bool m_isClosed;
        protected bool m_isRunningReceive;
        
        private CancellationTokenSource _cts;
        protected KcpConversationOptions m_kcpConversationOptions;
        private IKcpTransport<KcpConversation> _transport;
        private KcpConversation _conversation;

        private IKcpTransport<KcpConversation> _sendTransport;
        private KcpConversation _sendConversation;
        private UdpSocketServiceDispatcher<KcpService> _dispatcher;

        public virtual bool IsClosed
        {
            get
            {
                return m_isClosed;
            }
            protected set
            {
                if (m_isClosed == value)
                {
                    return;
                }
                m_isClosed = value;
            }
        }

        public virtual bool IsRunningReceive
        {
            get
            {
                return m_isRunningReceive;
            }
            protected set
            {
                if (m_isRunningReceive == value)
                {
                    return;
                }
                m_isRunningReceive = value;
            }
        }
        
        public KcpUdpReceiver(Socket socket, int mtu = 1400)
        {
            m_socket = socket;
            m_localEndPoint = m_socket.LocalEndPoint as IPEndPoint;
            m_addressFamily = m_socket.LocalEndPoint.AddressFamily;
            m_kcpConversationOptions = new KcpConversationOptions
            {
                Mtu = mtu, // Maximum Transmission Unit,
                NoDelay = true,
                DisableCongestionControl = true,
                UpdateInterval = 10,
            };
            
            if (OperatingSystem.IsWindows())
            {
                var IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                var SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                m_socket.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);
            }
        }

        // This is like "Start"
        public virtual void BeginReceiveFrom()
        {
            //Prevent call BeginReceiveFrom if it is already running
            if(m_isClosed && m_isRunningReceive)
            {
                m_isRunningReceive = false;
            }
            if (m_isRunningReceive || m_isClosed)
            {
                return;
            }
            
            m_isRunningReceive = true;
            _cts = new CancellationTokenSource();

            EndPoint recvEndPoint = m_addressFamily == AddressFamily.InterNetwork ? new IPEndPoint(IPAddress.Any, 0) : new IPEndPoint(IPAddress.IPv6Any, 0);
            _dispatcher = new UdpSocketServiceDispatcher<KcpService>(
                m_socket, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5),
                (sender, ep, state) =>
                {
                    var ojete = ep;
                    // For each peer connecting we create a KcpService instance.
                    try
                    {
                        var service = new KcpService(sender, ep,
                            ((Tuple<KcpConversationOptions, uint>)state!).Item1,
                            ((Tuple<KcpConversationOptions, uint>)state!).Item2);
                        service.OnDataReceived += ServiceOnOnDataReceived;
                        return service;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error creating service");
                        return null;
                    }
                },
                (service, state) => service.Dispose(),
                Tuple.Create(m_kcpConversationOptions, 0));

            _dispatcher.RunAsync(recvEndPoint, GC.AllocateUninitializedArray<byte>(m_kcpConversationOptions.Mtu), _cts.Token);

        }

        private void ServiceOnOnDataReceived(EndPoint endpoint, byte[] data)
        {
            CallOnPacketReceivedCallback(m_localEndPoint.Port, endpoint as IPEndPoint, data);
        }
        
        public IAsyncResult BeginSendTo(byte[] data, int offset, int size, SocketFlags socketFlags, EndPoint remoteEP,
            AsyncCallback callback, object state)
        {
            // Convert Task to APM 
            // https://devblogs.microsoft.com/pfxteam/using-tasks-to-implement-the-apm-pattern/
            return Send(data, remoteEP).ToApm(callback, null);
        }

        private async Task<bool> Send(byte[] data, EndPoint remoteEP)
        {
            if (_sendTransport == null)
            {
                var socket = new Socket(remoteEP.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                if (OperatingSystem.IsWindows())
                {
                    var IOC_IN = 0x80000000;
                    uint IOC_VENDOR = 0x18000000;
                    var SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                    socket.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);
                }
                await m_socket.ConnectAsync(remoteEP, _cts.Token);

                _sendTransport = KcpSocketTransport.CreateConversation(m_socket, remoteEP, 0, m_kcpConversationOptions);
                _sendTransport.Start();
                _sendConversation = _sendTransport.Connection;
            }
            
            var buffer = ArrayPool<byte>.Shared.Rent(data.Length);
            try
            {
                if (await _sendConversation.SendAsync(data.AsMemory(0, data.Length), _cts.Token))
                {
                    logger.LogDebug("Sent {DataLength} bytes", data.Length);
                    return true;
                }
                else
                {
                    logger.LogError("Error: Failed to send message");
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return false;
        }
        
        protected virtual void CallOnPacketReceivedCallback(int localPort, IPEndPoint remoteEndPoint, byte[] packet)
        {
            OnPacketReceived?.Invoke(this, localPort, remoteEndPoint, packet);
        }

        public virtual void Close(string reason)
        {
            if (!m_isClosed)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
                
                m_isClosed = true;
                m_socket?.Close();

                OnClosed?.Invoke(reason);
            }
        }
        
    }
}
