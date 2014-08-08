using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.dongxiguo.continuation.utils;
using haxe.lang;

namespace Rpc
{
    internal abstract class BaseWrappedHaxeIterator<Element> : Iterator<Element>
    {
        public abstract bool hasNext();

        public abstract Element next();

        public bool __hx_deleteField(string field, int hash)
        {
            throw new NotImplementedException();
        }

        public object __hx_lookupField(string field, int hash, bool throwErrors, bool isCheck)
        {
            throw new NotImplementedException();
        }

        public double __hx_lookupField_f(string field, int hash, bool throwErrors)
        {
            throw new NotImplementedException();
        }

        public object __hx_lookupSetField(string field, int hash, object value)
        {
            throw new NotImplementedException();
        }

        public double __hx_lookupSetField_f(string field, int hash, double value)
        {
            throw new NotImplementedException();
        }

        public double __hx_setField_f(string field, int hash, double value, bool handleProperties)
        {
            throw new NotImplementedException();
        }

        public object __hx_setField(string field, int hash, object value, bool handleProperties)
        {
            throw new NotImplementedException();
        }

        public object __hx_getField(string field, int hash, bool throwErrors, bool isCheck, bool handleProperties)
        {
            throw new NotImplementedException();
        }

        public double __hx_getField_f(string field, int hash, bool throwErrors, bool handleProperties)
        {
            throw new NotImplementedException();
        }

        public object __hx_invokeField(string field, int hash, Array dynargs)
        {
            throw new NotImplementedException();
        }

        public void __hx_getFields(Array<object> baseArr)
        {
            throw new NotImplementedException();
        }

        public object haxe_lang_Iterator_cast<T_c>()
        {
            throw new NotImplementedException();
        }
    }

    internal class WrappedHaxeIterator<Element> : BaseWrappedHaxeIterator<Element>
    {
        private BaseWrappedHaxeIterator<Element> wrappedHaxeIterator;

        public WrappedHaxeIterator(Object haxeIterator)
        {
            if (haxeIterator is Generator)
            {
                wrappedHaxeIterator = new WrappedHaxeGenerator<Element>((Generator<Element>)haxeIterator);
            }
            else
            {
                wrappedHaxeIterator = new WrappedReflectiveIteraor<Element>((Element)haxeIterator);
            }
        }

        public override bool hasNext()
        {
            return wrappedHaxeIterator.hasNext();
        }

        public override Element next()
        {
            return wrappedHaxeIterator.next();
        }

        private class WrappedHaxeGenerator<GeneratorElement> : BaseWrappedHaxeIterator<GeneratorElement>
        {
            public WrappedHaxeGenerator(Generator<GeneratorElement> haxeGenerator)
            {
                this.haxeGenerator = haxeGenerator;
            }

            private Generator<GeneratorElement> haxeGenerator;

            public override bool hasNext()
            {
                return haxeGenerator.hasNext();
            }

            public override GeneratorElement next()
            {
                var generator = haxeGenerator.next();
                return generator.value;
            }
        }

        private class WrappedReflectiveIteraor<Any> : BaseWrappedHaxeIterator<Any>
        {
            public WrappedReflectiveIteraor(Any haxeIterator)
            {
                this.haxeIterator = haxeIterator;
            }

            private Any haxeIterator;

            public override bool hasNext()
            {
                return (bool)Reflect.callMethod(haxeIterator, Reflect.field(haxeIterator, "hasNext"), new Array<Any>());
            }

            public override Any next()
            {
                return (Any)Reflect.callMethod(haxeIterator, Reflect.field(haxeIterator, "next"), new Array<Any>());
            }
        }

    }
}
