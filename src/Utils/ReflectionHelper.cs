using System.Reflection;

namespace FFUIOverhaul.Utils
{
    public static class ReflectionHelper
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        public static object? GetPrivateField<T>(string fieldName, T instance)
        {
            return typeof(T).GetField(fieldName, PrivateInstance)?.GetValue(instance);
        }

        public static void SetPrivateField<TObj, TVal>(string fieldName, TObj instance, TVal value)
        {
            typeof(TObj).GetField(fieldName, PrivateInstance)?.SetValue(instance, value);
        }

        public static object? CallPrivateMethod<T>(string methodName, T instance, object[]? args = null)
        {
            return typeof(T).GetMethod(methodName, PrivateInstance)?.Invoke(instance, args);
        }
    }
}
