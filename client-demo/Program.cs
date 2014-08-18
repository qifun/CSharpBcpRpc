using Bcp;
using BcpRpc;
using com.qifun.qforce.serverDemo1.entity;
using com.qifun.qforce.serverDemo1.xlsx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace client_demo
{
    class Program
    {
        class ClientPingPongImpl : IPingPong
        {
            private RpcSession<Bcp.BcpSession> rpcSession;

            public ClientPingPongImpl(RpcSession<Bcp.BcpSession> rpcSession)
            {
                this.rpcSession = rpcSession;
            }

            public Action<Action<Sheet2>, Action<object>> sendSheet1(Sheet1 request)
            {
                throw new NotImplementedException();
            }

            public Action<Action<Pong>, Action<object>> ping(Ping request)
            {
                return delegate(Action<Pong> pong, Action<object> obj)
                {
                    if (request.ping == "server_ping")
                    {
                        pong.Invoke(new Pong());
                    }
                };
            }

            public Action<Action<Pong>, Action<object>> pong(Pong response)
            {
                throw new NotImplementedException();
            }
        }

        public class PingPongRpcClient : TextSession<BcpSession>
        {
            public PingPongRpcClient(BcpSession bcpSession)
                : base(bcpSession)
            {
            }

            private static RpcSession<Bcp.BcpSession>.IncomingProxyRegistration<RpcSession<Bcp.BcpSession>> incomingServices = new IncomingProxyRegistration<RpcSession<BcpSession>>(
                new IncomingProxyEntry<RpcSession<BcpSession>>(
                    typeof(IPingPong),
                    (RpcSession<BcpSession> rpcSession) => com.qifun.qforce.serverDemo1.entity.IncomingProxyFactory.incomingProxy_com_qifun_qforce_serverDemo1_entity_IPingPong(new ClientPingPongImpl(rpcSession)))
                );

            protected override RpcSession<Bcp.BcpSession>.IncomingProxyRegistration<RpcSession<Bcp.BcpSession>> IncomingServices
            {
                get { return incomingServices; }
            }

            public class BcpClient : Bcp.BcpClient
            {
                public BcpClient()
                {
                    this.Received += OnReceived;
                }

                protected override Socket Connect()
                {
                    try
                    {
                        EndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.20"), 3333);
                        Socket socket = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        socket.Connect(ipEndPoint);
                        return socket;
                    }
                    catch
                    {
                        throw new SocketException();
                    }
                }

                private void OnReceived(object sender, ReceivedEventArgs e)
                {
                }
            }

            private readonly RpcSession<BcpSession>.OutgoingProxyEntry<IPingPong> PingPongEntry = new RpcSession<BcpSession>.OutgoingProxyEntry<IPingPong>(
                typeof(IPingPong),
                com.qifun.qforce.serverDemo1.entity.OutgoingProxyFactory.outgoingProxy_com_qifun_qforce_serverDemo1_entity_IPingPong);

            public void pingRequest()
            {
                var clientPingPong = this.OutgoingService(PingPongEntry);
                var clientPing = new Ping();
                clientPing.ping = "client_ping";
                Console.WriteLine("Client send ping request!");
                clientPingPong.ping(clientPing)(
                delegate(Pong response)
                {
                    if (response.pong == "server_pong")
                    {
                        Console.WriteLine("Success, client receive: " + response.pong);
                    }
                },
                delegate(object obj)
                {
                    Console.WriteLine("Fail, cient receive: " + obj);
                });
            }
        }

        static void Main(string[] args)
        {
            var client = new PingPongRpcClient(new PingPongRpcClient.BcpClient());
            client.pingRequest();
            while (true)
            {
                Thread.Sleep(10 * 1000);
            }
        }
    }
}
