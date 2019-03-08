namespace TACT.Net.Network
{
    /// <summary>
    /// A list of the common known Ribbit commands
    /// </summary>
    public enum RibbitCommand
    {
        /// <summary>
        /// A list of all products and their current sequence number
        /// </summary>
        Summary,
        /// <summary>
        /// Regional version information for a specific product
        /// </summary>
        Versions,
        /// <summary>
        /// Regional CDN sever information for a specific product
        /// </summary>
        CDNs,
        /// <summary>
        /// Version information for the Battle.net App background downloader
        /// </summary>
        Bgdl,
    }
}
