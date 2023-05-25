using System;
using System.Reflection;

namespace StaloTechAccum.CSharp.Misc
{
    /// <summary>
    /// 表示一个未绑定实例的方法。(not support IL2CPP)
    /// </summary>
    /// <remarks>该方法必须为一个 <c>class</c> 中的无参非静态函数。</remarks>
    public readonly unsafe struct UnboundClassMethod
    {
        private readonly void* m_FuncPtr;
        private readonly object m_MethodOrReturnType;

        public UnboundClassMethod(MethodInfo method)
        {
            if (method is null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            // TODO: Check whether the method uses unmanaged calling convention.

            if (method.DeclaringType.IsValueType)
            {
                throw new ArgumentException(
                    $"The declaring type of method {method.Name} should not be a value type.", nameof(method));
            }

            if (method.IsStatic)
            {
                throw new ArgumentException($"Method {method.Name} should not be static.", nameof(method));
            }

            if (method.IsAbstract || (method.IsGenericMethod && !method.IsConstructedGenericMethod))
            {
                throw new ArgumentException($"Method {method.Name} should be callable.", nameof(method));
            }

            if (method.GetParameters().Length > 0)
            {
                throw new ArgumentException($"Method {method.Name} should have no parameters.", nameof(method));
            }

            m_FuncPtr = (void*)method.MethodHandle.GetFunctionPointer();
            m_MethodOrReturnType = method.ReturnType switch
            {
                { IsValueType: true } => method,
                _ and var returnType => returnType
            };
        }

        public bool IsValid
        {
            get => m_FuncPtr is not null;
        }

        public bool IsReturnValueType
        {
            get => m_MethodOrReturnType is MethodInfo;
        }

        public T InvokeUnsafe<T>(object obj)
        {
            // 因为引用类型变量本质上都是一个 .NET 指针，
            // 所以可以简单地使用 object 代替所有引用类型。
            return ((delegate* managed<object, T>)m_FuncPtr)(obj);
        }

        public T Invoke<T>(object obj)
        {
            if (m_FuncPtr is null)
            {
                throw new InvalidOperationException(
                    $"Can not invoke {nameof(UnboundClassMethod)} constructed by the default .ctor.");
            }

            Type resultType = typeof(T);
            MethodInfo method = null;

            if (m_MethodOrReturnType is not Type actualType)
            {
                method = (MethodInfo)m_MethodOrReturnType;
                actualType = method.ReturnType;
            }

            if (!resultType.IsAssignableFrom(actualType))
            {
                throw new InvalidCastException($"Can not cast {actualType} to {resultType}.");
            }

            // 现在，如果满足下面的任意一个条件，那么函数可以直接被调用：
            // 1. 函数的返回值是引用类型。
            // 2. 函数的返回值是值类型且 T 就是这个值类型。

            if (method is null || resultType == actualType)
            {
                return InvokeUnsafe<T>(obj);
            }

            // 现在，函数的返回值一定是值类型，但 T 有以下几种情况：
            // 1. 基类：object、Enum 等。
            // 2. 接口：IEquatable<> 等。
            // 3. Nullable<>。
            //
            // 对于 case 1 和 2 肯定是要装箱的。一种手动装箱的方法是：
            // 先用 RuntimeHelpers.GetUninitializedObject 构造一个装箱后的对象，
            // 再用 GCHandle 固定对象并获取地址，将值类型变量的所有字节都拷贝过去。
            // 但是，目前找不到一个合适的方法把函数的返回值存到栈上，更确切地说
            // 是存到栈上手动开辟的内存中。
            //
            // 对于 case 3 也找不到合适的方法，只能先装箱再拆箱。
            //
            // 所以，只能这样调用了。
            return (T)method.Invoke(obj, parameters: null);
        }
    }
}