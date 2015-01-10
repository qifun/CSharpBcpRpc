using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Qifun.Bcp;
using Qifun.BcpRpc;
using System.Net;
using System.Net.Sockets;
using com.qifun.common.rpctest;
using Google.ProtocolBuffers;
using System.Threading;

namespace RpcTest
{
    abstract class TestServer : BcpServer
    {
        public static IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Loopback, 3333);

        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        public EndPoint LocalEndPoint;

        public TestServer()
        {
            serverSocket.Bind(ipEndPoint);
            serverSocket.Listen(100);
            LocalEndPoint = serverSocket.LocalEndPoint;
            startAccept();
        }

        private void startAccept()
        {
            serverSocket.BeginAccept(new AsyncCallback(acceptCallback), null);
        }

        private void acceptCallback(IAsyncResult ar)
        {
            try
            {
                Socket newSocket = serverSocket.EndAccept(ar);
                NetworkStream newStream = new NetworkStream(newSocket);
                AddIncomingSocket(newStream);
                startAccept();
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
    class Client : BcpClient
    {
        private EndPoint localEndPoint;
        public Client()
        {
            this.localEndPoint = new IPEndPoint(IPAddress.Loopback, 3333);
        }

        protected override Socket Connect()
        {
            try
            {
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
    [TestClass]
    public class RpcTest
    {
        static Object pingPongLock = new Object();
        volatile static String serverResult = null;
        volatile static String clientResult = null;
        volatile static String eventResult = null;
        class PingPongServer : TestServer
        {
            protected override BcpServer.Session NewSession(byte[] sessionId)
            {
                var session = new BcpServer.Session(this, sessionId);
                serverSession = new PingPongSession(session);
                return session;
            }

            public PingPongSession serverSession;

            public class PingPongSession : RpcSession
            {
                public PingPongSession(BcpSession session)
                    : base(session)
                {

                }

                private RpcSession.IncomingProxyRegistration incomingServices = new IncomingProxyRegistration(
                   new IncomingProxyEntry("com.qifun.common.rpctest", new Service())
                );

                protected override RpcSession.IncomingProxyRegistration IncomingServices
                {
                    get { return incomingServices; }
                }

                protected override RpcSession.ErrorCodeRegistration ErrorCodes
                {
                    get { return new RpcSession.ErrorCodeRegistration(typeof(RpcTestException)); }
                }
            }
            class Service : RpcService
            {
                private static IncomingMessageRegistration incomingMessage = new IncomingMessageRegistration(
                    new IncomingRequestEntry<RpcTestRequest, RpcTestResponse, PingPongServer.PingPongSession>(HandleRpcRequest)
                    );

                public override RpcService.IncomingMessageRegistration IncomingMessages
                {
                    get { return incomingMessage; }
                }

                private static RpcTestResponse HandleRpcRequest(RpcTestRequest message, PingPongServer.PingPongSession session)
                {
                    serverResult = message.Id + "";
                    var builder = new RpcTestResponse.Builder();
                    builder.SetId(710);
                    return builder.Build();
                }
            }
        }

        class PingPongClient : RpcSession
        {
            public PingPongClient(BcpSession session)
                : base(session)
            {

            }

            private static RpcSession.IncomingProxyRegistration incomingServices = new IncomingProxyRegistration(
                new IncomingProxyEntry("com.qifun.common.rpctest", new Service())
                );

            protected override RpcSession.IncomingProxyRegistration IncomingServices
            {
                get { return incomingServices; }
            }

            private static RpcSession.ErrorCodeRegistration errorCodeRegistration = new RpcSession.ErrorCodeRegistration(
                typeof(RpcTestException)
            );

            protected override RpcSession.ErrorCodeRegistration ErrorCodes
            {
                get { return errorCodeRegistration; }
            }
            class Service : RpcService
            {
                private static IncomingMessageRegistration incomingMessage = new IncomingMessageRegistration(
                    new IncomingMessageEntry<RpcTestEvent, PingPongClient>(HandleRpcEvent)
                    );

                public override RpcService.IncomingMessageRegistration IncomingMessages
                {
                    get { return incomingMessage; }
                }

                private static void HandleRpcEvent(RpcTestEvent message, PingPongClient client)
                {
                    lock (pingPongLock)
                    {
                        eventResult = message.Id + "";
                        Monitor.Pulse(pingPongLock);
                    }

                }
            }
        }
        [TestMethod]
        public void PingPong()
        {
            var server = new PingPongServer();
            var client = new PingPongClient(new Client());
            var aEvent = new RpcTestEvent.Builder();
            aEvent.SetId(922);
            var message = new RpcTestRequest.Builder();
            message.SetId(316);
            client.OutgoingService.SendRequest<RpcTestResponse>(message.Build(),
                delegate(RpcTestResponse response)
                {
                    lock (pingPongLock)
                    {
                        clientResult = response.Id + "";
                        Monitor.Pulse(pingPongLock);
                    }
                },
                delegate(IMessage fail)
                {
                    lock (pingPongLock)
                    {
                        clientResult = "wrong!";
                        if (fail is RpcTestException) { };
                        Monitor.Pulse(pingPongLock);
                    }
                });
            server.serverSession.OutgoingService.PushMessage(aEvent.Build());
            lock (pingPongLock)
            {
                while (serverResult == null || clientResult == null || eventResult == null)
                {
                    Monitor.Wait(pingPongLock);
                }
            }
            Assert.AreEqual(clientResult, "710");
            Assert.AreEqual(serverResult, "316");
            Assert.AreEqual(eventResult, "922");
            client.Session.ShutDown();
            server.serverSession.Session.ShutDown();
            server.Clear();
        }
    }
}
