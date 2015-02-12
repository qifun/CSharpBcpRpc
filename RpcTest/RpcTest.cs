using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Qifun.Bcp;
using Qifun.BcpRpc;
using System.Net;
using System.Net.Sockets;
using Google.ProtocolBuffers;
using System.Threading;
using Qifun.BcpRpc.Test;
using System.Diagnostics;

namespace RpcTest
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

        public Client(EndPoint localEndPoint)
        {
            this.localEndPoint = localEndPoint;
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
            public PingPongSession serverSession;

            protected override BcpServer.Session NewSession(byte[] sessionId)
            {
                var session = new BcpServer.Session(this, sessionId);
                lock (pingPongLock)
                {
                    serverSession = new PingPongSession(session);
                    Monitor.Pulse(pingPongLock);
                }
                return session;
            }

            public class PingPongSession : RpcSession
            {
                public PingPongSession(BcpSession session)
                    : base(session)
                {

                }

                private RpcSession.IncomingProxyRegistration incomingServices = new IncomingProxyRegistration(
                   new IncomingProxyEntry("Qifun.BcpRpc.Test", new Service())
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
                    serverResult = message.id + "";
                    var response = new RpcTestResponse();
                    response.id = 710;
                    return response;
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
                new IncomingProxyEntry("Qifun.BcpRpc.Test", new Service())
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
                        eventResult = message.id + "";
                        Monitor.Pulse(pingPongLock);
                    }

                }
            }
        }
        [TestMethod]
        public void PingPong()
        {
            var server = new PingPongServer();
            var client = new PingPongClient(new Client(server.LocalEndPoint));
            var aEvent = new RpcTestEvent();
            aEvent.id = 922;
            var message = new RpcTestRequest();
            message.id = 316;
            client.OutgoingService.SendRequest(message, typeof(RpcTestResponse),
                delegate(ProtoBuf.IExtensible response)
                {
                    var responseMessage = (RpcTestResponse)response;
                    lock (pingPongLock)
                    {
                        clientResult = responseMessage.id + "";
                        Monitor.Pulse(pingPongLock);
                    }
                },
                delegate(ProtoBuf.IExtensible fail)
                {
                    lock (pingPongLock)
                    {
                        clientResult = "wrong!";
                        if (fail is RpcTestException) { };
                        Monitor.Pulse(pingPongLock);
                    }
                });
            lock (pingPongLock)
            {
                while(server.serverSession == null)
                {
                    Monitor.Wait(pingPongLock);
                }
            }
            server.serverSession.OutgoingService.PushMessage(aEvent);
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
