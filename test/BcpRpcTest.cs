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
        static Object testLock = new Object();
        volatile static string serverResult = null;
        volatile static string clientResult = null;

        class ServerPingPongImpl : IPingPong
        {
            private RpcSession<BcpSession> rpcSession;

            public ServerPingPongImpl(RpcSession<BcpSession> rpcSession)
            {
                this.rpcSession = rpcSession;
            }

            public Action<Action<Sheet2>, Action<object>> sendSheet1(Sheet1 request)
            {
                throw new NotImplementedException();
            }

            public Action<Action<Pong>, Action<object>> ping(Ping request)
            {
                lock (testLock)
                {
                    serverResult = request.ping;
                    Monitor.Pulse(testLock);
                }
                return delegate(Action<Pong> responseHandler, Action<object> catcher)
                {
                    var serverPong = new Pong();
                    serverPong.pong = "pong";
                    responseHandler(serverPong);
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

            private class PingPongRpcSession : TextSession<BcpSession>
            {
                public PingPongRpcSession(BcpSession bcpSession)
                    : base(bcpSession)
                {
                }

                private static RpcSession<BcpSession>.IncomingProxyRegistration<RpcSession<BcpSession>> incomingServices = new IncomingProxyRegistration<RpcSession<BcpSession>>(
                    new IncomingProxyEntry<RpcSession<BcpSession>>(
                        typeof(IPingPong),
                        (RpcSession<BcpSession> rpcSession) => com.qifun.qforce.serverDemo1.entity.IncomingProxyFactory.incomingProxy_com_qifun_qforce_serverDemo1_entity_IPingPong(new ServerPingPongImpl(rpcSession)))
                    );

                protected override RpcSession<BcpSession>.IncomingProxyRegistration<RpcSession<BcpSession>> IncomingServices
                {
                    get { return incomingServices; }
                }
            }
        }

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
                throw new NotImplementedException();
            }

            public Action<Action<Pong>, Action<object>> pong(Pong response)
            {
                throw new NotImplementedException();
            }
        }

        public class PingPongRpcClient : TextSession<Bcp.BcpSession>
        {
            public PingPongRpcClient(BcpSession bcpSession)
                : base(bcpSession)
            {
            }

            private static RpcSession<BcpSession>.IncomingProxyRegistration<RpcSession<BcpSession>> incomingServices = new IncomingProxyRegistration<RpcSession<BcpSession>>(
                new IncomingProxyEntry<RpcSession<BcpSession>>(
                    typeof(IPingPong),
                    (RpcSession<BcpSession> rpcSession) => com.qifun.qforce.serverDemo1.entity.IncomingProxyFactory.incomingProxy_com_qifun_qforce_serverDemo1_entity_IPingPong(new ClientPingPongImpl(rpcSession)))
                );

            protected override RpcSession<BcpSession>.IncomingProxyRegistration<RpcSession<BcpSession>> IncomingServices
            {
                get { return incomingServices; }
            }

            public class PingPongClint : BcpClient
            {
                private EndPoint localEndPoint;
                public PingPongClint(EndPoint localEndPoint)
                {
                    this.localEndPoint = localEndPoint;
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

            }
        }

        private static readonly RpcSession<BcpSession>.OutgoingProxyEntry<IPingPong> PingPongEntry = new RpcSession<BcpSession>.OutgoingProxyEntry<IPingPong>(
            typeof(IPingPong),
            com.qifun.qforce.serverDemo1.entity.OutgoingProxyFactory.outgoingProxy_com_qifun_qforce_serverDemo1_entity_IPingPong);

        public static void pingRequest(PingPongRpcClient client)
        {
            var clientPingPong = client.OutgoingService(PingPongEntry);
            var clientPing = new Ping();
            clientPing.ping = "ping";
            clientPingPong.ping(clientPing)(
            delegate(Pong response)
            {
                lock (testLock)
                {
                    clientResult = response.pong;
                    Monitor.Pulse(testLock);
                }
            },
            delegate(object obj)
            {
                lock (testLock)
                {
                    clientResult = "fail";
                    Monitor.Pulse(testLock);
                }
            });
        }

        [TestMethod]
        public void TestMethod1()
        {
            var server = new PingPongServer();
            var client = new PingPongRpcClient(new PingPongRpcClient.PingPongClint(server.LocalEndPoint));
            pingRequest(client);
            lock (testLock)
            {
                while (clientResult == null || serverResult == null)
                {
                    Monitor.Wait(testLock);
                }
            }
            Assert.AreEqual(serverResult, "ping");
            Assert.AreEqual(clientResult, "pong");
            client.bcpSession.ShutDown();
            server.Clear();
        }
    }
}
