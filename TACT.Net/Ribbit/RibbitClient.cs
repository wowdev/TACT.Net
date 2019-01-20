using System;
using System.IO;
using System.Net.Sockets;
using MimeKit;

namespace TACT.Net.Ribbit
{
    public class RibbitClient
    {
        private const string Host = ".version.battle.net";

        private readonly string _endpoint;
        private readonly ushort _port;

        public RibbitClient(Locale locale, ushort port = 1119)
        {
            _endpoint = locale + Host;
            _port = port;
        }

        public string GetString(string payload)
        {
            using (var stream = new TcpClient(_endpoint, _port).GetStream())
            {
                stream.Write(System.Text.Encoding.ASCII.GetBytes(payload + "\r\n"));

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
            return new MemoryStream(System.Text.Encoding.ASCII.GetBytes(GetString(payload)));
        }

    }
}
