using System;
using System.Collections.Generic;
using System.Text;

namespace TACT.Net.Network
{
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
