using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using MimeKit;
using TACT.Net.Common;

namespace TACT.Net.Network
{
    /// <summary>
    /// A signed TCP implementation of retrieving product information
    /// <para>See https://wowdev.wiki/Ribbit</para>
    /// </summary>
    public sealed class RibbitClient
    {
        private const string Host = ".version.battle.net";

        private readonly string _endpoint;
        private readonly ushort _port;

        #region Constructors

        /// <summary>
        /// Creates a new Ribbit Client for the specified locale
        /// </summary>
        /// <param name="locale"></param>
        /// <param name="port"></param>
        public RibbitClient(Locale locale, ushort port = 1119)
        {
            if (locale == Locale.XX)
                throw new ArgumentException("Invalid locale", paramName: nameof(locale));

            _endpoint = locale + Host;
            _port = port;
        }

        #endregion

        #region Methods

        public async Task<string> GetString(string payload)
        {
            using (var stream = new TcpClient(_endpoint, _port).GetStream())
            {
                // apply the terminator
                if (!payload.EndsWith("\r\n"))
                    payload += "\r\n";

                await stream.WriteAsync(payload.GetBytes("ASCII")).ConfigureAwait(false);

                try
                {
                    var msg = await MimeMessage.LoadAsync(stream).ConfigureAwait(false);
                    return msg.TextBody;
                }
                catch (FormatException)
                {
                    return "";
                }
            }
        }

        public async Task<Stream> GetStream(string payload)
        {
            var response = await GetString(payload);
            return new MemoryStream(response.GetBytes("ASCII"));
        }

        public async Task<string> GetString(RibbitCommand command, string product)
        {
            return await GetString(CommandToPayload(command, product));
        }

        public async Task<Stream> GetStream(RibbitCommand command, string product)
        {
            return await GetStream(CommandToPayload(command, product));
        }

        #endregion

        #region Helpers

        private string CommandToPayload(RibbitCommand command, string product)
        {
            switch (command)
            {
                case RibbitCommand.Bgdl:
                    return $"v1/products/{product}/bgdl";
                case RibbitCommand.CDNs:
                    return $"v1/products/{product}/cdns";
                case RibbitCommand.Summary:
                    return $"v1/products/summary";
                case RibbitCommand.Versions:
                    return $"v1/products/{product}/versions";
            }

            return "";
        }

        #endregion
    }
}
