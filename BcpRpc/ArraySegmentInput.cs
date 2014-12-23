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
using System.Linq;
using System.Text;

namespace Qifun.BcpRpc
{
    internal class ArraySegmentInput
    {
        private IEnumerator<ArraySegment<Byte>> buffers;
        private ArraySegment<Byte> current = new ArraySegment<byte>();

        public ArraySegment<Byte> Current { get { return current; } }

        public ArraySegmentInput(IList<ArraySegment<Byte>> buffers)
        {
            this.buffers = buffers.GetEnumerator();
            if (buffers.Count != 0)
            {
                current = buffers.First();
            }
        }

        public int ReadByte()
        {
            if (current.Count == 0)
            {
                throw new RpcEofException();
            }
            else
            {
                var result = current.Array[current.Offset];
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

        public int ReadInt()
        {
            return (ReadByte() << 24) | (ReadByte() << 16) | (ReadByte() << 8) | ReadByte();
        }

        public int ReadBytes(byte[] s, int pos, int len)
        {
            if (current.Count == 0)
            {
                throw new RpcEofException();
            }
            else
            {
                if (len < current.Count)
                {
                    System.Array.Copy(Current.Array, Current.Offset, s, pos, len);
                    current = new ArraySegment<byte>(current.Array, current.Offset + len, current.Count - len);
                    return len;
                }
                else
                {
                    int result = current.Count;
                    System.Array.Copy(current.Array, current.Offset, s, pos, result);
                    if (buffers.MoveNext())
                    {
                        current = buffers.Current;
                    }
                    else
                    {
                        current = new ArraySegment<byte>();
                    }
                    return result;
                }
            }
        }

    }
}
