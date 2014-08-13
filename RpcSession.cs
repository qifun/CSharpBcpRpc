using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using com.dongxiguo.continuation.utils;
using haxe.lang;
using com.qifun.jsonStream;
using com.qifun.jsonStream.rpc;
using System.Diagnostics;
using Bcp;
using System.Threading;

namespace BcpRpc
{
    public abstract class RpcSession
    {

        public RpcSession(Bcp.BcpSession bcpSession)
        {
            bcpSession.Received += OnReceived;
            this.bcpSession = bcpSession;
        }
        private Bcp.BcpSession bcpSession;

        public class OutgoingProxyEntry<Service>
        {
            internal readonly System.Type serviceType;
            internal readonly RpcDelegate.OutgoingCallback<Service> outgoingView;

            public OutgoingProxyEntry(System.Type serviceType, RpcDelegate.OutgoingCallback<Service> outgoingView)
            {
                this.serviceType = serviceType;
                this.outgoingView = outgoingView;
            }
        }

        public interface IIncomingProxyEntry<Session>
        {

            string Name { get; }
            
            IJsonService NewJsonService(Session session);

        }

        public class IncomingProxyEntry<Session, Service> : IIncomingProxyEntry<Session>
        {
            internal readonly RpcDelegate.RpcFactoryCallback<Session, Service> rpcFactory;
            internal readonly System.Type serviceType;
            internal readonly RpcDelegate.IncomingViewCallback<Service> incomingView;

            public IncomingProxyEntry(
                RpcDelegate.RpcFactoryCallback<Session, Service> rpcFactory,
                System.Type serviceType,
                RpcDelegate.IncomingViewCallback<Service> incomingView)
            {
                this.rpcFactory = rpcFactory;
                this.serviceType = serviceType;
                this.incomingView = incomingView;
            }

            string IIncomingProxyEntry<Session>.Name
            {
                get { return serviceType.ToString(); }
            }

            IJsonService IIncomingProxyEntry<Session>.NewJsonService(Session session)
            {
                return incomingView(rpcFactory(session));
            }

        }

        public class IncomingProxyRegistration<Session>
        {
            internal Dictionary<string, RpcDelegate.IncomingProxyCallback<Session>> incomingProxyMap =
                new Dictionary<string, RpcDelegate.IncomingProxyCallback<Session>>();

            public IncomingProxyRegistration(params IIncomingProxyEntry<Session>[] incomingEntries)
            {
                foreach (var entry in incomingEntries)
                {
                    incomingProxyMap.Add(entry.Name, entry.NewJsonService);
                }
            }

        }

        internal class GeneratorFunction<Element> : Function
        {
            private Element element;

            public GeneratorFunction(int arity, int type, Element element)
                : base(arity, type)
            {
                this.element = element;
            }

            public object _hx_invoke2_o(double argumentValue0, object argumentRef0, double argumentValue1, object argumentRef1)
            {
                var yieldFunction = (Function)argumentRef0;
                var returnFunction = (Function)argumentRef1;
                return yieldFunction.__hx_invoke2_o(0, element, 0, returnFunction);
            }
        }

        private static Generator<Element> Generator1<Element>(Element element)
        {
            return new Generator<Element>(new GeneratorFunction<Element>(2, 0, element));
        }

        protected abstract IncomingProxyRegistration<RpcSession> IncomingServices();
        private int nextRequestId = -1;
        private Dictionary<int, IJsonResponseHandler> outgoingRpcResponseHandlers = new Dictionary<int, IJsonResponseHandler>();
        private object outgoingRpcResponseHandlerLock = new Object();

        protected abstract IList<ArraySegment<Byte>> ToByteBuffer(JsonStream js);

        protected abstract JsonStream ToJsonStream(IList<ArraySegment<Byte>> buffers);

        private class JsonService : IJsonService
        {
            private string serviceClassName;
            private RpcSession rpcSession;

            public JsonService(RpcSession rpcSession, string serviceClassName)
            {
                this.serviceClassName = serviceClassName;
                this.rpcSession = rpcSession;
            }

            public void apply(JsonStream request, IJsonResponseHandler handler)
            {
                int requestId = Interlocked.Increment(ref rpcSession.nextRequestId);
                IJsonResponseHandler oldHandler;
                lock (rpcSession.outgoingRpcResponseHandlerLock)
                {
                    if (rpcSession.outgoingRpcResponseHandlers.TryGetValue(requestId, out oldHandler))
                    {
                        throw new Exception("Illegal state!");
                    }
                    else
                    {
                        rpcSession.outgoingRpcResponseHandlers.Add(requestId, handler);
                    }
                }
                var requestStream = JsonStream.OBJECT(Generator1(new JsonStreamPair(
                    "request",
                    JsonStream.OBJECT(Generator1(new JsonStreamPair(
                        requestId.ToString(),
                        JsonStream.OBJECT(Generator1(new JsonStreamPair(serviceClassName, request)))))))));
                rpcSession.bcpSession.Send(rpcSession.ToByteBuffer(request));
            }
        }

        public ServiceInterface OutgoingService<ServiceInterface>(OutgoingProxyEntry<ServiceInterface> entry)
        {
            var serviceClassName = entry.serviceType.ToString();
            return entry.outgoingView(new JsonService(this, serviceClassName));
        }

        public class JsonResponseHandler : IJsonResponseHandler
        {
            private string id;
            private RpcSession rpcSession;

            public JsonResponseHandler(RpcSession rpcSession, string id)
            {
                this.rpcSession = rpcSession;
                this.id = id;
            }

            public void onSuccess(JsonStream responseBody)
            {
                var responseStream = JsonStream.OBJECT(Generator1(new JsonStreamPair(
                    "success",
                    JsonStream.OBJECT(Generator1(new JsonStreamPair(
                        id,
                        responseBody))))));
                rpcSession.bcpSession.Send(rpcSession.ToByteBuffer(responseStream));
            }

            public void onFailure(JsonStream errorBody)
            {
                var responseStream = JsonStream.OBJECT(Generator1(new JsonStreamPair(
                    "failure",
                    JsonStream.OBJECT(Generator1(new JsonStreamPair(
                        id,
                        errorBody))))));
                rpcSession.bcpSession.Send(rpcSession.ToByteBuffer(responseStream));
            }
        }

        private void OnReceived(object sender, BcpSession.ReceivedEventArgs e)
        {
            IList<ArraySegment<byte>> buffers = e.Buffers;
            var jsonStream = ToJsonStream(buffers);
            var requestOrResponsePairs = WrappedHaxeIterator.Wrap<JsonStreamPair>(jsonStream);
            while (requestOrResponsePairs.HasNext())
            {
                var requestOrResponsePair = (JsonStreamPair)requestOrResponsePairs.Next();
                switch (requestOrResponsePair.key)
                {
                    case "reqeust":
                        {
                            var idPaires = WrappedHaxeIterator.Wrap<JsonStreamPair>(requestOrResponsePair.value);
                            while (idPaires.HasNext())
                            {
                                var idPair = (JsonStreamPair)idPaires.Next();
                                var id = idPair.key;
                                var servicePairs = WrappedHaxeIterator.Wrap<JsonStreamPair>(idPair.value);
                                while (servicePairs.HasNext())
                                {
                                    var servicePair = (JsonStreamPair)servicePairs.Next();
                                    RpcDelegate.IncomingProxyCallback<RpcSession> incomingRpc;
                                    if (IncomingServices().incomingProxyMap.TryGetValue(servicePair.key, out incomingRpc))
                                    {
                                        incomingRpc(this).apply(servicePair.value, new JsonResponseHandler(this, id));
                                    }
                                    else
                                    {
                                        throw new UnknowServiceName();
                                    }
                                }
                            }
                        }
                        break;
                    case "failure":
                        {
                            var idPairs = WrappedHaxeIterator.Wrap<JsonStreamPair>(requestOrResponsePair.value);
                            while (idPairs.HasNext())
                            {
                                var idPair = (JsonStreamPair)idPairs.Next();
                                int id;
                                try
                                {
                                    id = Convert.ToInt32(idPair.key);
                                }
                                catch (Exception exception)
                                {
                                    throw new IllegalRpcData("", exception);
                                }
                                IJsonResponseHandler handler;
                                lock (outgoingRpcResponseHandlerLock)
                                {
                                    if (outgoingRpcResponseHandlers.TryGetValue(id, out handler))
                                    {
                                        outgoingRpcResponseHandlers.Remove(id);
                                    }
                                    else
                                    {
                                        throw new IllegalRpcData();
                                    }
                                }
                                handler.onFailure(idPair.value);
                            }
                        }
                        break;
                    case "success":
                        {
                            var idPairs = WrappedHaxeIterator.Wrap<JsonStreamPair>(requestOrResponsePair.value);
                            while (idPairs.HasNext())
                            {
                                var idPair = (JsonStreamPair)idPairs.Next();
                                int id;
                                try
                                {
                                    id = Convert.ToInt32(idPair.key);
                                }
                                catch (Exception exception)
                                {
                                    throw new IllegalRpcData("", exception);
                                }
                                IJsonResponseHandler handler;
                                lock (outgoingRpcResponseHandlerLock)
                                {
                                    if (outgoingRpcResponseHandlers.TryGetValue(id, out handler))
                                    {
                                        outgoingRpcResponseHandlers.Remove(id);
                                    }
                                    else
                                    {
                                        throw new IllegalRpcData();
                                    }
                                }
                                handler.onSuccess(idPair.value);
                            }
                        }
                        break;
                    default:
                        {
                            throw new IllegalRpcData();
                        }
                }
            }
        }
    }
}
