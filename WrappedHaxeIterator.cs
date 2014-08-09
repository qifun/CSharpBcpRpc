using com.dongxiguo.continuation.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BcpRpc
{
    class WrappedHaxeIterator<Element>
    {
        public static IWrappedHaxeIterator<Element> Wrap(Object haxeIterator)
        {
            if (haxeIterator is Generator)
            {
                return new WrappedHaxeGenerator((Generator<Element>)haxeIterator);
            }
            else
            {
                return new WrappedReflectiveIteraor((Element)haxeIterator);
            }
        }

        private sealed class WrappedHaxeGenerator : IWrappedHaxeIterator<Element>
        {
            private Generator<Element> haxeGenerator;

            public WrappedHaxeGenerator(Generator<Element> haxeGenerator)
            {
                this.haxeGenerator = haxeGenerator;
            }

            public bool HasNext()
            {
                return haxeGenerator.hasNext();
            }

            public Element Next()
            {
                return haxeGenerator.next().value;
            }
        }

        private sealed class WrappedReflectiveIteraor : IWrappedHaxeIterator<Element>
        {
            private object haxeIterator;

            public WrappedReflectiveIteraor(object haxeIterator)
            {
                this.haxeIterator = haxeIterator;
            }

            bool IWrappedHaxeIterator<Element>.HasNext()
            {
                return (bool)Reflect.callMethod(haxeIterator, Reflect.field(haxeIterator, "hasNext"), new Array<object>());
            }

            public Element Next()
            {
                return (Element)Reflect.callMethod(haxeIterator, Reflect.field(haxeIterator, "next"), new Array<object>());
            }
        }
    }
}
