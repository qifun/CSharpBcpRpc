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

using Google.ProtocolBuffers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Qifun.BcpRpc
{
    public abstract class IRpcService
    {

        public sealed class IncomingRequestEntry : IncomingEntry
        {
            private readonly RpcDelegate.RequestCallback requestCallback;
            public RpcDelegate.RequestCallback RequestCallback { get { return requestCallback; } }

            public IncomingRequestEntry(Type messageType, RpcDelegate.RequestCallback requestCallback)
                : base(messageType)
            {
                this.requestCallback = requestCallback;
            }

            public override void executeMessage(IMessage message, IRpcService service)
            {
                throw new NotImplementedException();
            }
        }

        // For Event, Info and CastRequest
        public sealed class IncomingMessageEntry<TMessage, TService> : IncomingEntry
            where TMessage : IMessage where TService : IRpcService
        {
            private readonly RpcDelegate.MessageCallback<TMessage, TService> messageCallback;

            public RpcDelegate.MessageCallback<TMessage, TService> MessageCallback { get { return messageCallback; } }

            public IncomingMessageEntry(RpcDelegate.MessageCallback<TMessage, TService> messageCallback)
                : base(typeof(TMessage))
            {
                this.messageCallback = messageCallback;
            }

            public override void executeMessage(IMessage message, IRpcService service)
            {
                messageCallback(message, service);
            }

        }

        public abstract class IncomingEntry {
            public IncomingEntry(Type messageType)
            {
                this.messageType = messageType;
            }
            
            private readonly Type messageType;
            public Type MessageType { get { return messageType; } }

            public abstract void executeMessage(IMessage message, IRpcService service);
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
