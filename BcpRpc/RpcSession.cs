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

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Qifun.Bcp;
using System.Threading;
using ProtoBuf;
using System.IO;

namespace Qifun.BcpRpc
{
    public abstract class RpcSession
    {

        public RpcSession(Bcp.BcpSession bcpSession) 
            : base()
        {
            outgoingProxy = new OutgoingProxy(this);
            this.bcpSession = bcpSession;
            this.bcpSession.Received += OnReceived;
        }
        private readonly Bcp.BcpSession bcpSession;
        private readonly OutgoingProxy outgoingProxy;
        
        public Bcp.BcpSession Session { get { return bcpSession; } }
        public OutgoingProxy OutgoingService { get { return outgoingProxy; } }

        protected sealed class IncomingProxyEntry
        {
            private string module;
            private RpcService incomingService;

            internal string Module { get { return module; } }
            internal RpcService IncomingService { get { return incomingService; } }

            public IncomingProxyEntry(string module, RpcService incomingService)
            {
                this.module = module;
                this.incomingService = incomingService;
            }

        }

        protected sealed class IncomingProxyRegistration
        {
            private readonly Dictionary<string, RpcService> incomingProxyMap = new Dictionary<string, RpcService>();

            public Dictionary<string, RpcService> IncomingProxyMap { get { return incomingProxyMap; } }

            public IncomingProxyRegistration(params IncomingProxyEntry[] incomingEntries)
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

        protected sealed class ErrorCodeRegistration
        {
            private readonly Dictionary<string, Type> errorCodeMap = new Dictionary<string, Type>();

            public Dictionary<string, Type> ErrorCodeMap { get { return errorCodeMap; } }

            public ErrorCodeRegistration(params Type[] errorCodes)
            {
                foreach(var errorCode in errorCodes)
                {
                    errorCodeMap.Add(errorCode.FullName, errorCode);
                }
            }
        }

        protected abstract ErrorCodeRegistration ErrorCodes
        {
            get;
        }

        public sealed class OutgoingProxy
        {
            private int nextMessageId = -1;
            internal Dictionary<int, IResponseHandler> outgoingRpcResponseHandlers = new Dictionary<int, IResponseHandler>();
            private object outgoingRpcResponseHandlerLock = new Object();
            private RpcSession rpcSession;

            public OutgoingProxy(RpcSession rpcSession)
            {
                this.rpcSession = rpcSession;
            }

            internal sealed class ResponseHandler : IResponseHandler
            {
                public ResponseHandler(Type responseType, Action<IExtensible> successCallback, Action<IExtensible> failCallback)
                {
                    this.responseType = responseType;
                    this.successCallback = successCallback;
                    this.failCallback = failCallback;
                }

                private readonly Action<IExtensible> successCallback;
                private readonly Action<IExtensible> failCallback;
                private readonly Type responseType;

                public Type ResponseType { get { return responseType; } }

                public void OnSuccess(IExtensible message)
                {
                    successCallback(message);
                }

                public void OnFailure(IExtensible message)
                {
                    failCallback(message);
                }
            }

            internal sealed class UIResponseHandler : IResponseHandler
            {
                public UIResponseHandler(
                    Type responseType, 
                    Action<IExtensible, Action<IExtensible>> successCallback,
                    Action<IExtensible, Action<IExtensible>> failCallback,
                    Action<IExtensible> uiSuccessCallback,
                    Action<IExtensible> uiFailCallback)
                {
                    this.responseType = responseType;
                    this.successCallback = successCallback;
                    this.failCallback = failCallback;
                    this.uiSuccessCallback = uiSuccessCallback;
                    this.uiFailCallback = uiFailCallback;
                }

                private readonly Type responseType;
                private readonly Action<IExtensible, Action<IExtensible>> successCallback;
                private readonly Action<IExtensible, Action<IExtensible>> failCallback;
                private readonly Action<IExtensible> uiSuccessCallback;
                private readonly Action<IExtensible> uiFailCallback;

                public Type ResponseType { get { return responseType; } }

                public void OnSuccess(IExtensible message)
                {
                    successCallback(message, uiSuccessCallback);
                }

                public void OnFailure(IExtensible message)
                {
                    failCallback(message, uiFailCallback);
                }
            }

            public void SendRequest(IExtensible message, Type responseType, Action<IExtensible> successCallback, Action<IExtensible> failCallback) 
            {
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

            public void SendRequest(
                IExtensible message, 
                Type responseType,
                Action<IExtensible, Action<IExtensible>> successCallback,
                Action<IExtensible, Action<IExtensible>> failCallback,
                Action<IExtensible> uiSuccessCallback,
                Action<IExtensible> uiFailCallback)
            {
                int messageId = Interlocked.Increment(ref nextMessageId);
                lock(outgoingRpcResponseHandlers)
                {
                    if(!outgoingRpcResponseHandlers.ContainsKey(messageId))
                    {
                        var responseHandler = new UIResponseHandler(responseType, successCallback, failCallback, uiSuccessCallback, uiFailCallback);
                        outgoingRpcResponseHandlers.Add(messageId, responseHandler);
                        rpcSession.SendMessage(BcpRpc.REQUEST, messageId, message);
                    }
                    else
                    {
                        throw new IllegalRpcData("Already contains message id.");
                    }
                }
            }

            public void PushMessage(IExtensible eventMessage)
            {
                int messageId = Interlocked.Increment(ref nextMessageId);
                rpcSession.SendMessage(BcpRpc.PUSHMESSAGE, messageId, eventMessage);
            }

        }

        private void SendMessage(int messageType, int messageId, IExtensible message)
        {
            var messageName = message.GetType().FullName;
            var nameSize = messageName.Length;
            byte[] messageByteArray;
            using(var stream = new MemoryStream())
            {
                Serializer.NonGeneric.Serialize(stream, message);
                messageByteArray = stream.ToArray();
            }
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

        private IExtensible BytesToMessage(ArraySegmentInput input, Type messageType, int messageSize)
        {
            if(messageSize > 0)
            {
                var messageBytes = new byte[messageSize];
                input.ReadBytes(messageBytes, 0, messageBytes.Length);
                object messageObject;
                using(var stream = new MemoryStream(messageBytes))
                {
                    messageObject = Serializer.NonGeneric.Deserialize(messageType, stream);
                }
                return (IExtensible)messageObject;
            }
            else
            {
                object messageObject;
                using(var stream = new MemoryStream())
                {
                    messageObject = Serializer.NonGeneric.Deserialize(messageType, stream);
                }
                return (IExtensible)messageObject;
            }
        }

        private void LogCantHandleMessage(string messageName)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.LogWarning("Illegal RPC data, can't handle such message: " + messageName);
#else
            Debug.WriteLine("Illegal RPC data, can't handle such message: " + messageName);
#endif
        }

        private void LogNotService(string packageName)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.LogWarning("Illegal RPC data, not such service: " + packageName);
#else
            Debug.WriteLine ("Illegal RPC data, not such service: " + packageName);
#endif
        }

        private void LogExecuteFail(Exception exception)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.LogWarning("Handle message fail: " + exception.StackTrace);
#else
            Debug.WriteLine("Handle message fail: " + exception.StackTrace);
#endif
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
            RpcService service;
            RpcService.IncomingEntry messageEntry;
            switch(messageType)
            {
                case BcpRpc.REQUEST:
                    {
                        if(IncomingServices.IncomingProxyMap.TryGetValue(packageName, out service))
                        {
                            if(service.IncomingMessages.IncomingMessageMap.TryGetValue(messageName, out messageEntry))
                            {
                                var message = BytesToMessage(input, messageEntry.MessageType, messageSize);
                                try
                                {
                                    var responseMessage = messageEntry.ExecuteRequest(message, this);
                                    SendMessage(BcpRpc.SUCCESS, messageId, responseMessage);
                                }
                                catch(ErrorCode exception)
                                {
                                    var errorMessage = exception.ProtobufMessage;
                                    SendMessage(BcpRpc.FAIL, messageId, errorMessage);
                                }
                                catch(Exception exception)
                                {
                                    LogExecuteFail(exception);
                                }
                            }
                            else
                            {
                                LogCantHandleMessage(messageName);
                            }
                        }
                        else
                        {
                            LogNotService(packageName);
                        }
                        break;
                    }
                case BcpRpc.PUSHMESSAGE:
                    {
                        if (IncomingServices.IncomingProxyMap.TryGetValue(packageName, out service))
                        {
                            if (service.IncomingMessages.IncomingMessageMap.TryGetValue(messageName, out messageEntry))
                            {
                                try
                                {
                                    var message = BytesToMessage(input, messageEntry.MessageType, messageSize);
                                    messageEntry.ExecuteMessage(message, this);
                                }
                                catch (Exception exception)
                                {
                                    LogExecuteFail(exception);
                                }
                            }
                            else
                            {
                                LogCantHandleMessage(messageName);
                            }
                        }
                        else
                        {
                            LogNotService(packageName);
                        }
                        break;
                    }
                case BcpRpc.SUCCESS:
                    {
                        IResponseHandler handler;
                        if(outgoingProxy.outgoingRpcResponseHandlers.TryGetValue(messageId, out handler))
                        {
                            try
                            {
                                outgoingProxy.outgoingRpcResponseHandlers.Remove(messageId);
                                var message = BytesToMessage(input, handler.ResponseType, messageSize);
                                handler.OnSuccess(message);
                            }
                            catch (Exception exception)
                            {
                                LogExecuteFail(exception);
                            }
                        }
                        else
                        {
                            LogCantHandleMessage(messageName);
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
                                try
                                {
                                    var message = BytesToMessage(input, errorType, messageSize);
                                    handler.OnFailure(message);
                                }
                                catch (Exception exception)
                                {
                                    LogExecuteFail(exception);
                                }
                            }
                            else
                            {
                                LogCantHandleMessage(messageName);
                            }
                        }
                        else
                        {
                            LogCantHandleMessage(messageName);
                        }
                        break;
                    }
                default:
                    {
                        LogCantHandleMessage(messageName);
                        break;
                    }
            }
        }

    }
}
