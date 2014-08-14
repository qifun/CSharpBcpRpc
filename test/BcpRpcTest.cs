using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Bcp;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using BcpRpc;
using System.Collections;
using com.qifun.jsonStream.rpc;
using com.qifun.qforce.serverDemo1.entity;
using com.qifun.qforce.serverDemo1.xlsx;
using System.Threading;

namespace test
{
    abstract class TestServer : BcpServer
    {
        public static IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Loopback, 0);

        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        public EndPoint LocalEndPoint;

        public TestServer()
        {
            serverSocket.Bind(ipEndPoint);
            serverSocket.Listen(100);
            LocalEndPoint = serverSocket.LocalEndPoint;
            Debug.WriteLine("Listening: " + serverSocket.LocalEndPoint);
            StartAccept();
        }

        private void StartAccept()
        {
            serverSocket.BeginAccept(new AsyncCallback(AcceptCallback), null);
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                Socket newSocket = serverSocket.EndAccept(ar);
                NetworkStream newStream = new NetworkStream(newSocket);
                AddIncomingSocket(newStream);
                StartAccept();
            }
            catch
            {
            }
        }

        public void Clear()
        {
            serverSocket.Dispose();
        }
    }

    [TestClass]
    public class BcpRpcTest
    {
        class ServerPingPongImpl : IPingPong
        {
            private RpcSession rpcSession;

            public ServerPingPongImpl(RpcSession rpcSession)
            {
                this.rpcSession = rpcSession;
            }

            public Action<Action<Sheet2>, Action<object>> sendSheet1(Sheet1 request)
            {
                throw new NotImplementedException();
            }

            public Action<Action<Pong>, Action<object>> ping(Ping request)
            {
                return delegate(Action<Pong> responseHandler, Action<object> catcher)
                {
                    responseHandler(new Pong());
                };
            }

            public Action<Action<Pong>, Action<object>> pong(Pong response)
            {
                throw new NotImplementedException();
            }
        }

        private class PingPongServer : TestServer
        {
            protected override BcpServer.Session NewSession(byte[] sessionId)
            {
                var session = new BcpServer.Session(this, sessionId);
                var rpcSession = new PingPongRpcSession(session);
                return session;
            }

            private class PingPongRpcSession : TextSession
            {
                public PingPongRpcSession(BcpSession bcpSession)
                    : base(bcpSession)
                {
                }

                private static RpcSession.IncomingProxyRegistration<RpcSession> incomingServices = new IncomingProxyRegistration<RpcSession>(
                    new IncomingProxyEntry<RpcSession>(
                        typeof(IPingPong),
                        (RpcSession rpcSession) => com.qifun.qforce.serverDemo1.entity.IncomingProxyFactory.incomingProxy_com_qifun_qforce_serverDemo1_entity_IPingPong(new ServerPingPongImpl(rpcSession)))
                    );

                protected override RpcSession.IncomingProxyRegistration<RpcSession> IncomingServices()
                {
                    return incomingServices;
                }
            }
        }

        class ClientPingPongImpl : IPingPong
        {
            private RpcSession rpcSession;

            public ClientPingPongImpl(RpcSession rpcSession)
            {
                this.rpcSession = rpcSession;
            }

            public Action<Action<Sheet2>, Action<object>> sendSheet1(Sheet1 request)
            {
                throw new NotImplementedException();
            }

            public Action<Action<Pong>, Action<object>> ping(Ping request)
            {
                throw new NotImplementedException();
            }

            public Action<Action<Pong>, Action<object>> pong(Pong response)
            {
                throw new NotImplementedException();
            }
        }

        class PingPongClint : BcpClient
        {
            private EndPoint localEndPoint;

            public class PingPongRpcSession : TextSession
            {

                public PingPongRpcSession(BcpSession bcpSession)
                    : base(bcpSession)
                {
                }

                protected override RpcSession.IncomingProxyRegistration<RpcSession> IncomingServices()
                {
                    return new IncomingProxyRegistration<RpcSession>(
                        new IncomingProxyEntry<RpcSession>(
                            typeof(IPingPong),
                            (RpcSession rpcSession) => 
                                com.qifun.qforce.serverDemo1.entity.IncomingProxyFactory.incomingProxy_com_qifun_qforce_serverDemo1_entity_IPingPong(new ClientPingPongImpl(this)))
                    );
                }
            }
            public PingPongRpcSession rpcSession;

            public PingPongClint(EndPoint localEndPoint)
            {
                rpcSession = new PingPongRpcSession(this);
                this.localEndPoint = localEndPoint;
                this.Received += OnReceived;
            }

            protected override Socket Connect()
            {
                try
                {
                    Debug.WriteLine("Connecting...");
                    EndPoint ep = localEndPoint;
                    Socket socket = new Socket(ep.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    socket.Connect(ep);
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

        readonly RpcSession.OutgoingProxyEntry<IPingPong> PingPongEntry = new RpcSession.OutgoingProxyEntry<IPingPong>(
            typeof(IPingPong),
            com.qifun.qforce.serverDemo1.entity.OutgoingProxyFactory.outgoingProxy_com_qifun_qforce_serverDemo1_entity_IPingPong);

        [TestMethod]
        public void TestMethod1()
        {
            var server = new PingPongServer();
            var client = new PingPongClint(server.LocalEndPoint);
            var clientPingPong = client.rpcSession.OutgoingService(PingPongEntry);
            clientPingPong.ping(new Ping())(
            delegate(Pong pong)
            {
                Console.WriteLine("client receive: " + pong.pong);
            },
            delegate(object obj)
            {
            });
            Thread.Sleep(3 * 1000);
        }
    }
}
