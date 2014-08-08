using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using haxe.io;
using haxe.lang;

namespace Rpc
{
    internal class ArraySegmentInput : Input
    {
        private IEnumerator<ArraySegment<Byte>> buffers;
        private ArraySegment<Byte> current = new ArraySegment<byte>();

        public ArraySegmentInput(IList<ArraySegment<Byte>> buffers)
        {
            this.buffers = buffers.GetEnumerator();
            if (buffers.Count != 0)
            {
                current = buffers.First();
            }
        }

        public override int readByte()
        {
            if (current.Count == 0)
            {
                throw HaxeException.wrap(new Eof());
            }
            else
            {
                var result = current.First();
                current = new ArraySegment<byte>(current.Array, current.Offset + 1, current.Count - 1);
                if (current.Count == 0)
                {
                    if (buffers.MoveNext())
                    {
                        current = buffers.Current;
                    }
                }
                return result;
            }
        }

        public override int readBytes(Bytes s, int pos, int len)
        {
            if (current.Count == 0)
            {
                throw HaxeException.wrap(new Eof());
            }
            else
            {
                if (len < current.Count)
                {
                    byte[] bytes = (new ArraySegment<byte>(current.Array, current.Offset, len)).Array;
                    System.Array.Copy(bytes, 0, s.getData(), pos, len);
                    current = new ArraySegment<byte>(current.Array, current.Offset + len, current.Count - len);
                    return len;
                }
                else
                {
                    int result = current.Count;
                    byte[] bytes = current.ToArray();
                    System.Array.Copy(bytes, 0, s.getData(), pos, result);
                    current = new ArraySegment<byte>();
                    if (buffers.MoveNext())
                    {
                        current = buffers.Current;
                    }
                    return result;
                }
            }
        }
    }
}
