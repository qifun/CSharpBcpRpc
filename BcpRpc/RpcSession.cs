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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Qifun.Bcp;
using System.Threading;
using Google.ProtocolBuffers;

namespace Qifun.BcpRpc
{
    public abstract class RpcSession<BcpSession> where BcpSession : Bcp.BcpSession
    {

        public RpcSession(Bcp.BcpSession bcpSession) 
            : base()
        {
            outgoingProxy = new OutgoingProxy(this);
            this.bcpSession = bcpSession;
            this.bcpSession.Received += OnReceived;
        }
        public readonly Bcp.BcpSession bcpSession;
        public readonly OutgoingProxy outgoingProxy;

        public sealed class IncomingProxyEntry<TService> where TService : IRpcService
        {
            private string module;
            private TService incomingService;

            internal string Module { get { return module; } }
            internal TService IncomingService { get { return incomingService; } }

            public IncomingProxyEntry(string module, TService incomingService)
            {
                this.module = module;
                this.incomingService = incomingService;
            }

        }

        public sealed class IncomingProxyRegistration
        {
            private readonly Dictionary<string, IRpcService> incomingProxyMap = new Dictionary<string, IRpcService>();

            public Dictionary<string, IRpcService> IncomingProxyMap { get { return incomingProxyMap; } }

            public IncomingProxyRegistration(params IncomingProxyEntry<IRpcService>[] incomingEntries)
            {
                foreach (var entry in incomingEntries)
                {
                    incomingProxyMap.Add(entry.Module, entry.IncomingService);
                }
            }
        }

        protected abstract IncomingProxyRegistration IncomingServices
        {
            get;
        }

        public sealed class ErrorCodeRgistration
        {
            private readonly Dictionary<string, Type> errorCodeMap = new Dictionary<string, Type>();

            public Dictionary<string, Type> ErrorCodeMap { get { return errorCodeMap; } }

            public ErrorCodeRgistration(params Type[] errorCodes)
            {
                foreach(var errorCode in errorCodes)
                {
                    errorCodeMap.Add(errorCode.FullName, errorCode);
                }
            }
        }

        protected abstract ErrorCodeRgistration ErrorCodes
        {
            get;
        }

        public sealed class OutgoingProxy
        {
            private int nextMessageId = -1;
            internal Dictionary<int, IResponseHandler> outgoingRpcResponseHandlers = new Dictionary<int, IResponseHandler>();
            private object outgoingRpcResponseHandlerLock = new Object();
            private RpcSession<BcpSession> rpcSession;

            public OutgoingProxy(RpcSession<BcpSession> rpcSession)
            {
                this.rpcSession = rpcSession;
            }

            internal sealed class ResponseHandler : IResponseHandler
            {
                public ResponseHandler(Type responseType, Action<IMessage> successCallback, Action<IMessage> failCallback)
                {
                    this.responseType = responseType;
                    this.successCallback = successCallback;
                    this.failCallback = failCallback;
                }

                private readonly Action<IMessage> successCallback;
                private readonly Action<IMessage> failCallback;
                private readonly Type responseType;

                public Type ResponseType { get { return responseType; } }

                public void OnSuccess(IMessage message)
                {
                    successCallback(message);
                }

                public void OnFailure(IMessage message)
                {
                    failCallback(message);
                }
            }

            public void SendRequest<TResponseMessage>(IMessage message, Action<IMessage> successCallback, Action<IMessage> failCallback) 
                where TResponseMessage : IMessage
            {
                Type responseType = typeof(TResponseMessage);
                int messageId = Interlocked.Increment(ref nextMessageId);
                lock (outgoingRpcResponseHandlerLock)
                {
                    if(!outgoingRpcResponseHandlers.ContainsKey(messageId))
                    {
                        var responseHandler = new ResponseHandler(responseType, successCallback, failCallback);
                        outgoingRpcResponseHandlers.Add(messageId, responseHandler);
                        rpcSession.SendMessage(BcpRpc.REQUEST, messageId, message);
                    }
                    else
                    {
                        throw new IllegalRpcData("");
                    }
                }
            }

            public void SendEvent(IMessage eventMessage)
            {
                int messageId = Interlocked.Increment(ref nextMessageId);
                rpcSession.SendMessage(BcpRpc.EVENT, messageId, eventMessage);
            }

            public void SendInfo(IMessage info)
            {
                int messageId = Interlocked.Increment(ref nextMessageId);
                rpcSession.SendMessage(BcpRpc.INFO, messageId, info);
            }

            public void SendCastRequest(IMessage castRequest)
            {
                int messageId = Interlocked.Increment(ref nextMessageId);
                rpcSession.SendMessage(BcpRpc.CASTREQUEST, messageId, castRequest);
            }


        }

        private void SendMessage(int messageType, int messageId, IMessage message)
        {
            var messageName = message.GetType().FullName;
            var nameSize = messageName.Length;
            var messageByteArray = message.ToByteArray();
            var messageSize = messageByteArray.Length;
            var output = new ArraySegmentOutput();
            var messageNameBytes = Encoding.UTF8.GetBytes(messageName);
            output.WriteInt(messageId);
            output.WriteByte(messageType);
            output.WriteByte(nameSize);
            output.WriteInt(messageSize);
            output.WriteBytes(messageNameBytes, 0, messageNameBytes.Length);
            output.WriteBytes(messageByteArray, 0, messageByteArray.Length);
            bcpSession.Send(output.Buffers);
        }

        private IMessage BytesToMessage(ArraySegmentInput input, Type messageType, int messageSize)
        {
            if(messageSize > 0)
            {
                var messageBytes = new byte[messageSize];
                input.ReadBytes(messageBytes, 0, messageBytes.Length);
                var messageObject = messageType.GetProperty("DefaultInstance").GetValue(null, null);
                var parseFrom = messageType.GetMethod("ParseFrom", new Type[] { typeof(byte[]) });
                return (IMessage)parseFrom.Invoke(messageObject, new object[] { messageBytes });
            }
            else
            {
                return (IMessage)Activator.CreateInstance(messageType);
            }
        }

        private void OnReceived(object sender, Bcp.BcpSession.ReceivedEventArgs e)
        {
            var input = new ArraySegmentInput(e.Buffers);
            var messageId = input.ReadInt();
            var messageType = input.ReadByte();
            var messageNameSize = input.ReadByte();
            var messageSize = input.ReadInt();
            var messageNameBytes = new byte[messageNameSize];
            input.ReadBytes(messageNameBytes, 0, messageNameBytes.Length);
            var messageName = Encoding.UTF8.GetString(messageNameBytes);
            var packageName = messageName.Substring(0, messageName.LastIndexOf('.'));
            IRpcService service;
            IRpcService.IncomingMessageEntry messageEntry;
            switch(messageType)
            {
                case BcpRpc.REQUEST:
                    {
                        if(IncomingServices.IncomingProxyMap.TryGetValue(packageName, out service))
                        {
                            if(service.IncomingMessages.IncomingMessageMap.TryGetValue(messageName, out messageEntry))
                            {
                                var message = BytesToMessage(input, messageEntry.MessageType, messageSize);
                                messageEntry.RequestCallback(message, service);
                                // TODO Handle response
                            }
                            else
                            {
                                this.bcpSession.Interrupt();
                                Debug.WriteLine("Illegal RPC data!");
                            }
                        }
                        else
                        {
                            this.bcpSession.Interrupt();
                            Debug.WriteLine("Illegal RPC data!");
                        }
                        break;
                    }
                case BcpRpc.CASTREQUEST:
                    {
                        // Handle cast request
                        break;
                    }
                case BcpRpc.EVENT:
                    {
                        if(IncomingServices.IncomingProxyMap.TryGetValue(packageName, out service))
                        {
                            if(service.IncomingMessages.IncomingMessageMap.TryGetValue(messageName, out messageEntry))
                            {
                                var message = BytesToMessage(input, messageEntry.MessageType, messageSize);
                                messageEntry.EvnetCallback(message, service);
                                // TODO Handle response
                            }
                            else
                            {
                                this.bcpSession.Interrupt();
                                Debug.WriteLine("Illegal RPC data!");
                            }
                        }
                        else
                        {
                            this.bcpSession.Interrupt();
                            Debug.WriteLine("Illegal RPC data!");
                        }
                        break;
                    }
                case BcpRpc.INFO:
                    {
                        if(IncomingServices.IncomingProxyMap.TryGetValue(packageName, out service))
                        {
                            if(service.IncomingMessages.IncomingMessageMap.TryGetValue(messageName, out messageEntry))
                            {
                                var message = BytesToMessage(input, messageEntry.MessageType, messageSize);
                                messageEntry.InfoCallback(message, service);
                                // TODO Handle response
                            }
                            else
                            {
                                this.bcpSession.Interrupt();
                                Debug.WriteLine("Illegal RPC data!");
                            }
                        }
                        else
                        {
                            this.bcpSession.Interrupt();
                            Debug.WriteLine("Illegal RPC data!");
                        }
                        break;
                    }
                case BcpRpc.SUCCESS:
                    {
                        IResponseHandler handler;
                        if(outgoingProxy.outgoingRpcResponseHandlers.TryGetValue(messageId, out handler))
                        {
                            outgoingProxy.outgoingRpcResponseHandlers.Remove(messageId);
                            var message = BytesToMessage(input, handler.ResponseType, messageSize);
                            handler.OnSuccess(message);
                        }
                        else
                        {
                            this.bcpSession.Interrupt();
                            Debug.WriteLine("Illegal RPC data!");
                        }
                        break;
                    }
                case BcpRpc.FAIL:
                    {
                        IResponseHandler handler;
                        if(outgoingProxy.outgoingRpcResponseHandlers.TryGetValue(messageId, out handler))
                        {
                            outgoingProxy.outgoingRpcResponseHandlers.Remove(messageId);
                            Type errorType;
                            if (ErrorCodes.ErrorCodeMap.TryGetValue(messageName, out errorType))
                            {
                                var message = (IMessage)Activator.CreateInstance(errorType);
                                handler.OnFailure(message);
                            }
                            else
                            {
                                this.bcpSession.Interrupt();
                                Debug.WriteLine("Illegal RPC error code!");
                            }
                        }
                        else
                        {
                            this.bcpSession.Interrupt();
                            Debug.WriteLine("Illegal RPC data!");
                        }
                        break;
                    }
                default:
                    {
                        this.bcpSession.Interrupt();
						Debug.WriteLine("Illegal RPC data!");
                        break;
                    }
            }
        }

    }
}
