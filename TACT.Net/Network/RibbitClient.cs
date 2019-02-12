using System;
using System.IO;
using System.Net.Sockets;
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

        public string GetString(RibbitCommand command, string product)
        {
            return GetString(CommandToPayload(command, product));
        }

        public Stream GetStream(RibbitCommand command, string product)
        {
            return GetStream(CommandToPayload(command, product));
        }

        public string GetString(string payload)
        {
            using (var stream = new TcpClient(_endpoint, _port).GetStream())
            {
                stream.Write((payload + "\r\n").GetBytes("ASCII"));

                try
                {
                    return MimeMessage.Load(stream).TextBody;
                }
                catch (FormatException)
                {
                    return "";
                }
            }
        }

        public Stream GetStream(string payload)
        {
            return new MemoryStream(GetString(payload).GetBytes("ASCII"));
        }

        #endregion

        #region Helpers

        private string CommandToPayload(RibbitCommand command, string product)
        {
            switch(command)
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
