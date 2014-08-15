using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using com.qifun.jsonStream.rpc;

namespace BcpRpc
{
    public class RpcDelegate
    {
        public delegate IJsonService IncomingProxyCallback<Session>(Session session);

        public delegate Service OutgoingCallback<Service>(IJsonService jsonService);
    }
}
