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

        public RibbitClient(Locale locale, ushort port = 1119)
        {
            _endpoint = locale + Host;
            _port = port;
        }

        #endregion

        #region Methods

        public async Task<string> GetString(string payload)
        {
            using (var stream = new TcpClient(_endpoint, _port).GetStream())
            {
                await stream.WriteAsync((payload + "\r\n").GetBytes("ASCII")).ConfigureAwait(false);

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
