using System;
using System.Collections.Generic;
using TACT.Net.Common.Cryptography;

namespace TACT.Net
{
    public sealed class TACT
    {
        public readonly string BaseDirectory;

        private readonly Dictionary<Type, object> _referenceStore;

        #region Constructors

        public TACT()
        {
            _referenceStore = new Dictionary<Type, object>();
        }

        public TACT(string baseDirectory) : this()
        {
            BaseDirectory = baseDirectory;
        }

        #endregion

        #region Reference store

        internal void Inject<T>(T reference) where T : class
        {
            _referenceStore[typeof(T)] = reference;
        }

        /// <summary>
        /// Returns a reference to type <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T Resolve<T>() where T : class
        {
            _referenceStore.TryGetValue(typeof(T), out var reference);
            return reference as T;
        }

        /// <summary>
        /// Returns a reference to type <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reference"></param>
        /// <returns></returns>
        public bool TryResolve<T>(out T reference) where T : class
        {
            bool result = _referenceStore.TryGetValue(typeof(T), out var _ref);
            reference = _ref as T;
            return result;
        }

        /// <summary>
        /// Determines whether this reference is stored or not
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool HasReference<T>() where T : class => _referenceStore.ContainsKey(typeof(T));

        #endregion

        #region Methods

        ///// <summary>
        ///// Creates a new TACT container populated with the necessary base files
        ///// </summary>
        ///// <param name="product"></param>
        ///// <param name="locale"></param>
        //public void Create(string product, Locale locale)
        //{
        //    new Configs.ConfigContainer(product, this);
        //    new Archives.ArchiveContainer(this);
        //    new Root.RootFile(this);
        //    new Encoding.EncodingFile(this);
        //    new Install.InstallFile(this);
        //    new Download.DownloadFile(this);
        //}

        ///// <summary>
        ///// Opens an existing TACT container and loads the Root and Encoding files
        ///// </summary>
        ///// <param name="directory"></param>
        ///// <param name="product"></param>
        ///// <param name="locale"></param>
        //public void Open(string directory, string product, Locale locale)
        //{
        //    var configContainer = new Configs.ConfigContainer(product, this);
        //    configContainer.Open(directory, locale);

        //    var archiveContainer = new Archives.ArchiveContainer(this);
        //    archiveContainer.Open(directory);

        //    new Root.RootFile(archiveContainer.OpenFile(configContainer.RootMD5), this);
        //    new Encoding.EncodingFile(archiveContainer.OpenFile(configContainer.EncodingEKey), this);
        //}


        #endregion
    }
}
