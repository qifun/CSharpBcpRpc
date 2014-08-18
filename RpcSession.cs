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

        public sealed class OutgoingProxyEntry<Service>
        {
            internal readonly System.Type serviceType;
            internal readonly RpcDelegate.OutgoingCallback<Service> outgoingView;

            public OutgoingProxyEntry(System.Type serviceType, RpcDelegate.OutgoingCallback<Service> outgoingView)
            {
                this.serviceType = serviceType;
                this.outgoingView = outgoingView;
            }
        }

        public sealed class IncomingProxyEntry<Session>
        {
            internal readonly RpcDelegate.IncomingProxyCallback<Session> rpcFactory;
            internal readonly System.Type serviceType;

            public IncomingProxyEntry(
                System.Type serviceType,
                RpcDelegate.IncomingProxyCallback<Session> rpcFactory)
            {
                this.rpcFactory = rpcFactory;
                this.serviceType = serviceType;
            }

        }

        public sealed class IncomingProxyRegistration<Session>
        {
            internal Dictionary<string, RpcDelegate.IncomingProxyCallback<Session>> incomingProxyMap =
                new Dictionary<string, RpcDelegate.IncomingProxyCallback<Session>>();

            public IncomingProxyRegistration(params IncomingProxyEntry<Session>[] incomingEntries)
            {
                foreach (var entry in incomingEntries)
                {
                    incomingProxyMap.Add(entry.serviceType.ToString(), entry.rpcFactory);
                }
            }

        }

        private class GeneratorFunction<Element> : Function
        {
            private Element element;

            public GeneratorFunction(int arity, int type, Element element)
                : base(arity, type)
            {
                this.element = element;
            }

            override public object __hx_invoke2_o(double argumentValue0, object argumentRef0, double argumentValue1, object argumentRef1)
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

        protected abstract IncomingProxyRegistration<RpcSession> IncomingServices
        {
            get;
        }

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
                        throw new InvalidOperationException();
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
                rpcSession.bcpSession.Send(rpcSession.ToByteBuffer(requestStream));
            }
        }

        public ServiceInterface OutgoingService<ServiceInterface>(OutgoingProxyEntry<ServiceInterface> entry)
        {
            var serviceClassName = entry.serviceType.ToString();
            return entry.outgoingView(new JsonService(this, serviceClassName));
        }

        public sealed class JsonResponseHandler : IJsonResponseHandler
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

        private static bool ReflectHasNext(object iterator)
        {
            return Runtime.toBool(Reflect.callMethod(iterator, Reflect.field(iterator, "hasNext"), new Array<object>()));
        }

        private static Element ReflectNext<Element>(object iterator)
        {
            return Runtime.genericCast<Element>(Reflect.callMethod(iterator, Reflect.field(iterator, "next"), new Array<object>()));
        }

        private static int JsonStreamObjectIndex = Type.getEnumConstructs(typeof(JsonStream)).indexOf("OBJECT", Null<int>._ofDynamic(0));

        private void OnReceived(object sender, BcpSession.ReceivedEventArgs e)
        {
            var jsonStream = ToJsonStream(e.Buffers);
            if (Type.enumIndex(jsonStream) == JsonStreamObjectIndex)
            {
                var requestOrResponsePairs = Type.enumParameters(jsonStream)[0];
                while (ReflectHasNext(requestOrResponsePairs))
                {
                    var requestOrResponsePair = ReflectNext<JsonStreamPair>(requestOrResponsePairs);
                    if (Type.enumIndex(requestOrResponsePair.value) == JsonStreamObjectIndex)
                    {
                        switch (requestOrResponsePair.key)
                        {
                            case "request":
                                {
                                    var idPaires = Type.enumParameters(requestOrResponsePair.value)[0];
                                    while (ReflectHasNext(idPaires))
                                    {
                                        var idPair = ReflectNext<JsonStreamPair>(idPaires);
                                        var id = idPair.key;
                                        if (Type.enumIndex(idPair.value) == JsonStreamObjectIndex)
                                        {
                                            var servicePairs = Type.enumParameters(idPair.value)[0];
                                            while (ReflectHasNext(servicePairs))
                                            {
                                                var servicePair = ReflectNext<JsonStreamPair>(servicePairs);
                                                RpcDelegate.IncomingProxyCallback<RpcSession> incomingRpc;
                                                if (IncomingServices.incomingProxyMap.TryGetValue(servicePair.key, out incomingRpc))
                                                {
                                                    incomingRpc(this).apply(servicePair.value, new JsonResponseHandler(this, id));
                                                }
                                                else
                                                {
                                                    throw new UnknowServiceName();
                                                }
                                            }
                                        }
                                        else
                                        {
                                            throw new IllegalRpcData();
                                        }
                                    }
                                }
                                break;
                            case "failure":
                                {
                                    var idPairs = Type.enumParameters(requestOrResponsePair.value)[0];
                                    while (ReflectHasNext(idPairs))
                                    {
                                        var idPair = ReflectNext<JsonStreamPair>(idPairs);
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
                                    var idPairs = Type.enumParameters(requestOrResponsePair.value)[0];
                                    while (ReflectHasNext(idPairs))
                                    {
                                        var idPair = ReflectNext<JsonStreamPair>(idPairs);
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
