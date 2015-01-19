/*
 * CSharpBcpRpc
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

using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Qifun.BcpRpc
{
    public abstract class RpcService
    {
        public abstract class IncomingEntry
        {
            public IncomingEntry(Type messageType)
            {
                this.messageType = messageType;
            }

            private readonly Type messageType;
            public Type MessageType { get { return messageType; } }

            public abstract IExtensible ExecuteRequest(IExtensible message, RpcSession session);

            public abstract void ExecuteMessage(ProtoBuf.IExtensible message, RpcSession session);

        }

        public sealed class IncomingRequestEntry<TRequestMessage, TResponseMessage, TSession> : IncomingEntry
            where TRequestMessage : IExtensible 
            where TResponseMessage : IExtensible
            where TSession : RpcSession
        {
            private readonly RpcDelegate.RequestCallback<TRequestMessage, TResponseMessage, TSession> requestCallback;

            public IncomingRequestEntry(RpcDelegate.RequestCallback<TRequestMessage, TResponseMessage, TSession> requestCallback)
                : base(typeof(TRequestMessage))
            {
                this.requestCallback = requestCallback;
            }

            public override IExtensible ExecuteRequest(IExtensible message, RpcSession session)
            {
                return requestCallback((TRequestMessage)message, (TSession)session);
            }

            public override void ExecuteMessage(IExtensible message, RpcSession session)
            {
                throw new NotImplementedException();
            }
        }

        public sealed class IncomingMessageEntry<TMessage, TSession> : IncomingEntry
            where TMessage : IExtensible where TSession : RpcSession
        {
            private readonly RpcDelegate.MessageCallback<TMessage, TSession> messageCallback;

            public IncomingMessageEntry(RpcDelegate.MessageCallback<TMessage, TSession> messageCallback)
                : base(typeof(TMessage))
            {
                this.messageCallback = messageCallback;
            }

            public override IExtensible ExecuteRequest(IExtensible message, RpcSession session)
            {
                throw new NotImplementedException();
            }

            public override void ExecuteMessage(IExtensible message, RpcSession session)
            {
                messageCallback((TMessage)message, (TSession)session);
            }

        }


        public sealed class IncomingMessageRegistration
        {
            private readonly Dictionary<string, IncomingEntry> incomingMessageMap = new Dictionary<string, IncomingEntry>();

            internal Dictionary<string, IncomingEntry> IncomingMessageMap { get { return incomingMessageMap; } }

            public IncomingMessageRegistration(params IncomingEntry[] incomingMessages)
            {
                foreach(var incomingMessage in incomingMessages)
                {
                    incomingMessageMap.Add(incomingMessage.MessageType.FullName, incomingMessage);
                }
            }
        }

        public abstract IncomingMessageRegistration IncomingMessages
        {
            get;
        }

    }
}
