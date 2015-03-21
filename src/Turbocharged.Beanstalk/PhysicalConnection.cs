﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Turbocharged.Beanstalk
{
    class PhysicalConnection : IDisposable
    {
        TcpClient _client;
        NetworkStream _stream;
        IDisposable _receiveTask;
        SemaphoreSlim _insertionLock = new SemaphoreSlim(1, 1);

        BlockingCollection<Request> _requestsAwaitingResponse =
            new BlockingCollection<Request>(new ConcurrentQueue<Request>());

        PhysicalConnection()
        {
            _client = new TcpClient();
        }

        public static async Task<PhysicalConnection> ConnectAsync(string hostname, int port)
        {
            var conn = new PhysicalConnection();
            await conn._client.ConnectAsync(hostname, port).ConfigureAwait(false);
            conn._stream = conn._client.GetStream();
            var cts = new CancellationTokenSource();

#pragma warning disable 4014
            conn.ReceiveAsync(cts.Token);
#pragma warning restore 4014

            conn._receiveTask = Disposable.Create(() =>
            {
                cts.Cancel();
                cts.Dispose();
            });

            return conn;
        }

        public void Dispose()
        {
            using (_client)
            using (_stream)
            {
                _receiveTask.Dispose();
                _client.Close();
            }
        }

        public async Task SendAsync<T>(Request<T> request, CancellationToken cancellationToken)
        {
            await _insertionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _requestsAwaitingResponse.Add(request);
                var data = request.ToByteArray();
                await _stream.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _insertionLock.Release();
            }
        }

        public async Task ReceiveAsync(CancellationToken token)
        {
            try
            {
                var firstLineMaxLength = 255;
                byte[] buffer = new byte[firstLineMaxLength];
                int pos = -1; // Our current position in the buffer
                while (true)
                {
                    pos++;

                    // So this is kind of dumb. But since I can't use a
                    // StreamReader (can't get raw bytes out of it)
                    // or a BinaryReader (no async methods) and I don't
                    // want to deal with buffering myself, I'm reading
                    // one character at a time so I don't read past the CRLF
                    await _stream.ReadAsync(buffer, pos, 1, token).ConfigureAwait(false);

                    // We're done if the last two characters were CR LF
                    if (pos > 0 && buffer[pos - 1] == 13 && buffer[pos] == 10)
                    {
                        // We have a line, make it a string and send it to whoever is waiting for it.
                        var incoming = buffer.ToASCIIString(0, pos - 1);
                        var request = _requestsAwaitingResponse.Take(token);
                        try
                        {
                            request.Process(incoming, _stream);
                        }
                        catch (Exception)
                        {
                            // How rude
                        }
                        pos = -1; // Overwrite the buffer on the next go-round
                    }
                    else if (pos == firstLineMaxLength)
                    {
                        // Oops
                        break;
                    }
                }
            }
            catch (Exception)
            {
            }

            // No way to really recover from exiting this loop
            Drain();
            Dispose();
        }

        void Drain()
        {
            Request request;
            while (_requestsAwaitingResponse.TryTake(out request))
            {
                request.Cancel();
            }
        }
    }
}
