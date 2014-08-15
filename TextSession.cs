using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using haxe.lang;
using com.qifun.jsonStream.io;
using com.qifun.jsonStream;

namespace BcpRpc
{
    public abstract class TextSession : RpcSession
    {
        public TextSession(Bcp.BcpSession bcpSession)
            : base(bcpSession)
        {
        }

        protected override IList<ArraySegment<byte>> ToByteBuffer(com.qifun.jsonStream.JsonStream js)
        {
            var output = new ArraySegmentOutput();
            PrettyTextPrinter.print(output, js, Null<int>._ofDynamic(0));
            return output.Buffers;
        }

        protected override JsonStream ToJsonStream(IList<ArraySegment<byte>> buffers)
        {
            var arraySegmentInput = new ArraySegmentInput(buffers);
            JsonStream jsonStream = null;
            try
            {
                jsonStream = TextParser.parseInput(new ArraySegmentInput(buffers));
            }
            catch (Exception e)
            {
                var current = Encoding.Default.GetString(arraySegmentInput.Current.ToArray());
                throw new ParseTextException("Parse exception at: " + current, e);
            }
            return jsonStream;
        }
    }
}
