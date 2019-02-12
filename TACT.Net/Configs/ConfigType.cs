namespace TACT.Net.Configs
{
    /// <summary>
    /// Config Types
    /// </summary>
    public enum ConfigType
    {
        /// <summary>
        /// A list of the required data and patch archives
        /// </summary>
        CDNConfig,
        /// <summary>
        /// Primarily documents the key system files
        /// </summary>
        BuildConfig,
        /// <summary>
        /// Documents information for patching the client
        /// </summary>
        PatchConfig,
        /// <summary>
        /// Regional version information for a specific product
        /// </summary>
        Versions,
        /// <summary>
        /// Regional CDN sever information for a specific product
        /// </summary>
        CDNs,
    }
}
