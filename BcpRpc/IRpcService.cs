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
