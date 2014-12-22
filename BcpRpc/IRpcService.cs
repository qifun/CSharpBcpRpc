using Google.ProtocolBuffers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Qifun.BcpRpc
{
    public abstract class IRpcService
    {

        public sealed class IncomingMessageEntry
        {
            private readonly Type messageType;
            private readonly RpcDelegate.RequestCallback requestCallback;
            private readonly RpcDelegate.InfoCallback infoCallback;
            private readonly RpcDelegate.EventCallback eventCallback;
            private readonly RpcDelegate.CastRequestCallback castRequestCallback;
            private readonly int incomingMessageType;

            public Type MessageType { get { return messageType; } }
            public RpcDelegate.RequestCallback RequestCallback { get { return requestCallback; } }
            public RpcDelegate.InfoCallback InfoCallback { get { return infoCallback; } }
            public RpcDelegate.EventCallback  EvnetCallback { get { return eventCallback; } }
            public RpcDelegate.CastRequestCallback CastRequestCallback { get { return castRequestCallback; } }
            public int IncomingMessageType { get { return incomingMessageType; } }

            public IncomingMessageEntry(Type messageType, RpcDelegate.RequestCallback requestCallback)
            {
                this.messageType = messageType;
                this.requestCallback = requestCallback;
                this.incomingMessageType = BcpRpc.REQUEST;
            }

            public IncomingMessageEntry(Type messageType, RpcDelegate.InfoCallback infoCallback)
            {
                this.messageType = messageType;
                this.infoCallback = infoCallback;
                this.incomingMessageType = BcpRpc.INFO;
            }

            public IncomingMessageEntry(Type messageType, RpcDelegate.EventCallback eventCallback)
            {
                this.messageType = messageType;
                this.eventCallback = eventCallback;
                this.incomingMessageType = BcpRpc.EVENT;
            }

            public IncomingMessageEntry(Type messageType, RpcDelegate.CastRequestCallback castRequestCallback)
            {
                this.messageType = messageType;
                this.castRequestCallback = castRequestCallback;
                this.incomingMessageType = BcpRpc.CASTREQUEST;
            }
        }

        public sealed class IncomingMessageRegistration
        {
            private readonly Dictionary<string, IncomingMessageEntry> incomingMessageMap = new Dictionary<string, IncomingMessageEntry>();

            internal Dictionary<string, IncomingMessageEntry> IncomingMessageMap { get { return incomingMessageMap; } }

            public IncomingMessageRegistration(params IncomingMessageEntry[] incomingMessages)
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
