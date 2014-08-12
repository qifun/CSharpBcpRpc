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

    [TestClass]
    public class BcpRpcTest
    {

        private class PingPongImpl : IPingPong
        {
            private RpcSession rpcSession;
            public PingPongImpl(RpcSession rpcSession)
            {
                this.rpcSession = rpcSession;
            }

            public com.qifun.jsonStream.rpc._Future.IFuture<object> sendSheet1(com.qifun.qforce.serverDemo1.xlsx.Sheet1 request)
            {
                throw new NotImplementedException();
            }

            public com.qifun.jsonStream.rpc._Future.IFuture<object> ping(Ping request)
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

                protected override RpcSession.IncomingProxyRegistration<RpcSession> IncomingServices()
                {
                    throw new NotImplementedException();
                }
            }
        }

        class PingPongClint : BcpClient
        {
            private EndPoint localEndPoint;

            private class PingPongRpcSession : TextSession
            {
                private PingPongImpl pingPongImpl;

                public PingPongRpcSession(BcpSession bcpSession)
                    : base(bcpSession)
                {
                    pingPongImpl = new PingPongImpl(this);
                }

                protected override RpcSession.IncomingProxyRegistration<RpcSession> IncomingServices()
                {
                    return new IncomingProxyRegistration<PingPongRpcSession>(new System.Collections.Generic.List<IncomingProxyEntry<PingPongRpcSession, Object>> {
                        new IncomingProxyEntry<PingPongRpcSession, PingPongImpl>(
                            delegate(PingPongRpcSession rpcSession) { return pingPongImpl; },
                            typeof(IPingPong),
                            com.qifun.qforce.serverDemo1.entity.IncomingProxyFactory.incomingProxy_com_qifun_qforce_serverDemo1_entity_IPingPong),
                    });
                }
            }
            private PingPongRpcSession pingPongRpcSession;

            public PingPongClint(EndPoint localEndPoint)
            {
                pingPongRpcSession = new PingPongRpcSession(this);
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

        [TestMethod]
        public void TestMethod1()
        {
        }
    }
}
