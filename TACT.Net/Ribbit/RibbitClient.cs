using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using MimeKit;

namespace TACT.Net.Ribbit
{
    public class RibbitClient : IDisposable
    {
        private const string Host = ".version.battle.net";

        private readonly TcpClient _tcpClient;

        public RibbitClient(Locale locale, ushort port = 1119)
        {
            _tcpClient = new TcpClient(locale + Host, port);
        }

        public string GetString(string payload)
        {
            using (NetworkStream stream = _tcpClient.GetStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                byte[] req = System.Text.Encoding.ASCII.GetBytes(payload + "\r\n");
                stream.Write(req);

                try
                {
                    return MimeMessage.Load(stream).TextBody;
                }
                catch(FormatException)
                {
                    return "";
                }
            }
        }

        public Stream GetStream(string payload)
        {
            return new MemoryStream(System.Text.Encoding.ASCII.GetBytes(GetString(payload)));
        }

        public void Dispose()
        {
            _tcpClient.Dispose();
        }
    }
}
