using System;

namespace TACT.Net.Common.Singleton
{
    public class Singleton<T> where T : class, new()
    {
        public static T Instance => lazy.Value;

        private static readonly Lazy<T> lazy = new Lazy<T>(() => new T());

        internal Singleton() { }
    }
}
