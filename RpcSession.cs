using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using com.dongxiguo.continuation.utils;
using haxe.lang;
using com.qifun.jsonStream;
using com.qifun.jsonStream.rpc;
using System.Diagnostics;

namespace BcpRpc
{
    public abstract class RpcSession : Bcp.BcpClient
    {
        public class OutgoingProxyEntry<Service>
        {
            internal Type serviceType;
            internal RpcDelegate.OutgoingCallback<Service> outgoingView;

            public OutgoingProxyEntry(Type serviceType, RpcDelegate.OutgoingCallback<Service> outgoingView)
            {
                this.serviceType = serviceType;
                this.outgoingView = outgoingView;
            }
        }

        public class IncomingProxyEntry<Session, Service>
        {
            internal RpcDelegate.RpcFactoryCallback<Session, Service> rpcFactory;
            internal Type serviceType;
            internal RpcDelegate.IncomingViewCallback<Service> incomingView;

            public IncomingProxyEntry(
                RpcDelegate.RpcFactoryCallback<Session, Service> rpcFactory,
                Type serviceType,
                RpcDelegate.IncomingViewCallback<Service> incomingView)
            {
                this.rpcFactory = rpcFactory;
                this.serviceType = serviceType;
                this.incomingView = incomingView;
            }
        }

        public class IncomingProxyRegistration<Session>
        {
            internal Dictionary<string, RpcDelegate.IncomingProxyCallback<Session>> incomingProxyMap =
                new Dictionary<string, RpcDelegate.IncomingProxyCallback<Session>>();

            public IncomingProxyRegistration(IList<IncomingProxyEntry<Session, Object>> incomingEntries)
            {
                foreach (var entry in incomingEntries)
                {
                    incomingProxyMap.Add(entry.serviceType.ToString(), incomingRpc(entry));
                }
            }

            private RpcDelegate.IncomingProxyCallback<Session> incomingRpc<Service>(
                RpcDelegate.RpcFactoryCallback<Session, Service> rpcFactory,
                RpcDelegate.IncomingViewCallback<Service> incomingView)
            {
                return delegate(Session session)
                {
                    return incomingView(rpcFactory(session));
                };
            }

            private RpcDelegate.IncomingProxyCallback<Session> incomingRpc<Service>(IncomingProxyEntry<Session, Service> entry)
            {
                return incomingRpc(entry.rpcFactory, entry.incomingView);
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

        private static Generator<Element> generator1<Element>(Element element)
        {
            return new Generator<Element>(new GeneratorFunction<Element>(2, 0, element));
        }

        protected abstract IncomingProxyRegistration<RpcSession> incomingServices();
        private int nextRequestId = 0;
        private Dictionary<int, IJsonResponseHandler> outgoingRpcResponseHandlers = new Dictionary<int, IJsonResponseHandler>();

        protected abstract IList<ArraySegment<Byte>> ToByteBuffer(JsonStream js);

        protected abstract JsonStream ToJsonStream(IList<ArraySegment<Byte>> buffers);

        internal Object rpcSessionLock = new Object();

        private class JsonService : IJsonService
        {
            private int requestId;
            private string serviceClassName;
            private RpcSession rpcSession;

            public JsonService(RpcSession rpcSession, int requestId, string serviceClassName)
            {
                this.requestId = requestId;
                this.serviceClassName = serviceClassName;
                this.rpcSession = rpcSession;
            }

            public void apply(JsonStream request, IJsonResponseHandler handler)
            {
                var requestStream = JsonStream.OBJECT(generator1(new JsonStreamPair(
                    "request",
                    JsonStream.OBJECT(generator1(new JsonStreamPair(
                        requestId.ToString(),
                        JsonStream.OBJECT(generator1(new JsonStreamPair(serviceClassName, request)))))))));
                rpcSession.Send(rpcSession.ToByteBuffer(request));
            }
        }

        public ServiceInterface OutgoingService<ServiceInterface>(OutgoingProxyEntry<ServiceInterface> entry)
        {
            var serviceClassName = entry.serviceType.ToString();
            lock (rpcSessionLock)
            {
                int requestId = nextRequestId;
                nextRequestId += 1;
                if (!outgoingRpcResponseHandlers.ContainsKey(requestId))
                {
                    return entry.outgoingView(new JsonService(this, requestId, serviceClassName));
                }
                else
                {
                    throw new Exception("Illegal state!");
                }
            }
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
                var responseStream = JsonStream.OBJECT(generator1(new JsonStreamPair(
                    "success",
                    JsonStream.OBJECT(generator1(new JsonStreamPair(
                        id,
                        responseBody))))));
                rpcSession.Send(rpcSession.ToByteBuffer(responseStream));
            }

            public void onFailure(JsonStream errorBody)
            {
                var responseStream = JsonStream.OBJECT(generator1(new JsonStreamPair(
                    "failure",
                    JsonStream.OBJECT(generator1(new JsonStreamPair(
                        id,
                        errorBody))))));
                rpcSession.Send(rpcSession.ToByteBuffer(responseStream));
            }
        }

        protected override void Received(IList<ArraySegment<byte>> buffers)
        {
            var jsonStream = ToJsonStream(buffers);
            var requestOrResponsePairs = IWrappedHaxeIterator<JsonStreamPair>.Wrap(jsonStream);
            while (requestOrResponsePairs.HasNext())
            {
                var requestOrResponsePair = (JsonStreamPair)requestOrResponsePairs.Next();
                if (requestOrResponsePair.key.Equals("request"))
                {
                    var idPaires = IWrappedHaxeIterator<JsonStreamPair>.Wrap(requestOrResponsePair.value);
                    while (idPaires.HasNext())
                    {
                        var idPair = (JsonStreamPair)idPaires.Next();
                        var id = idPair.key;
                        var servicePairs = IWrappedHaxeIterator<JsonStreamPair>.Wrap(idPair.value);
                        while (servicePairs.HasNext())
                        {
                            var servicePair = (JsonStreamPair)servicePairs.Next();
                            RpcDelegate.IncomingProxyCallback<RpcSession> incomingRpc;
                            if (incomingServices().incomingProxyMap.TryGetValue(servicePair.key, out incomingRpc))
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
                else if (requestOrResponsePair.key.Equals("failure"))
                {
                    var idPairs = IWrappedHaxeIterator<JsonStreamPair>.Wrap(requestOrResponsePair.value);
                    while (idPairs.HasNext())
                    {
                        var idPair = (JsonStreamPair)idPairs.Next();
                        int id;
                        try
                        {
                            id = Convert.ToInt32(idPair.key);
                        }
                        catch (Exception e)
                        {
                            throw new IllegalRpcData("", e);
                        }
                        IJsonResponseHandler handler;
                        if (outgoingRpcResponseHandlers.TryGetValue(id, out handler))
                        {
                            outgoingRpcResponseHandlers.Remove(id);
                            handler.onFailure(idPair.value);
                        }
                        else
                        {
                            throw new IllegalRpcData();
                        }
                    }
                }
                else if (requestOrResponsePair.key.Equals("success"))
                {
                    var idPairs = IWrappedHaxeIterator<JsonStreamPair>.Wrap(requestOrResponsePair.value);
                    while (idPairs.HasNext())
                    {
                        var idPair = (JsonStreamPair)idPairs.Next();
                        int id;
                        try
                        {
                            id = Convert.ToInt32(idPair.key);
                        }
                        catch (Exception e)
                        {
                            throw new IllegalRpcData("", e);
                        }
                        IJsonResponseHandler handler;
                        if (outgoingRpcResponseHandlers.TryGetValue(id, out handler))
                        {
                            outgoingRpcResponseHandlers.Remove(id);
                            handler.onSuccess(idPair.value);
                        }
                        else
                        {
                            throw new IllegalRpcData();
                        }
                    }
                }
                else
                {
                    throw new IllegalRpcData();
                }
            }
        }
    }
}
