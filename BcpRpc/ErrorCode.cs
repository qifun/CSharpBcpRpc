using Google.ProtocolBuffers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Qifun.BcpRpc
{
    public class ErrorCode: Exception
    {
        private IMessage message;
        public IMessage ProtobufMessage { get { return message; } }
        public ErrorCode(IMessage message)
        {
            this.message = message;
        }
    }
}
