using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Google.ProtocolBuffers;

namespace Qifun.BcpRpc
{
    internal interface IResponseHandler
    {
        Type ResponseType { get; }

        void OnSuccess(IMessage message);

        void OnFailure(IMessage message);

    }
}
