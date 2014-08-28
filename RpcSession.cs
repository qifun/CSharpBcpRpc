/*
 * csharp-bcp-rpc
 * Copyright 2014 深圳岂凡网络有限公司 (Shenzhen QiFun Network Corp., LTD)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

﻿using System;
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
    public abstract class RpcSession<BcpSession> where BcpSession : Bcp.BcpSession
    {

        public RpcSession()
        {
        }

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

        internal static Generator<Element> Generator1<Element>(Element element)
        {
            return new Generator<Element>(new GeneratorFunction<Element>(2, 0, element));
        }

        protected abstract IncomingProxyRegistration<RpcSession<BcpSession>> IncomingServices
        {
            get;
        }

        internal int nextRequestId = -1;
        internal Dictionary<int, IJsonResponseHandler> outgoingRpcResponseHandlers = new Dictionary<int, IJsonResponseHandler>();
        internal object outgoingRpcResponseHandlerLock = new Object();

        protected abstract IList<ArraySegment<Byte>> ToByteBuffer(JsonStream js);

        protected abstract JsonStream ToJsonStream(IList<ArraySegment<Byte>> buffers);

    }
}
