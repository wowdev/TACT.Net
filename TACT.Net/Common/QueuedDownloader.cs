using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TACT.Net.Network;

namespace TACT.Net.Common
{
    internal class QueuedDownloader
    {
        private readonly string _directory;
        private readonly CDNClient _client;
        private readonly ConcurrentQueue<string> _queue;

        public QueuedDownloader(string directory, CDNClient client)
        {
            _directory = directory;
            _client = client;
            _queue = new ConcurrentQueue<string>();
        }


        public void Enqueue(string file)
        {
            _queue.Enqueue(file);
        }

        public void Enqueue(IEnumerable<string> files)
        {
            foreach (var file in files)
                _queue.Enqueue(file);
        }

        public void Enqueue<T>(IEnumerable<T> entries, Func<T, string> nameFunc)
        {
            foreach (var entry in entries)
                _queue.Enqueue(nameFunc(entry));
        }


        public void Download(string folder)
        {
            folder = folder.ToLower();

            var tasks = new List<Task>(Environment.ProcessorCount);

            for (int i = 0; i < tasks.Count; i++)
            {
                var task = Task.Run(async () =>
                {
                    while (_queue.TryDequeue(out var item))
                    {
                        string url = Helpers.GetCDNUrl(item, folder);
                        string filepath = Helpers.GetCDNPath(item, folder, _directory, true);

                        if (!File.Exists(filepath))
                            await _client.DownloadFile(url, filepath);
                    }
                });

                tasks.Add(task);
            }

            Task.WhenAll(tasks).Wait();
        }
    }
}
