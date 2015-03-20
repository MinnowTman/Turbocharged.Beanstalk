﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Turbocharged.Beanstalk
{
    class ReserveRequest : Request<Job>
    {
        public Task<Job> Task { get { return _tcs.Task; } }

        TaskCompletionSource<Job> _tcs = new TaskCompletionSource<Job>();
        TimeSpan? _timeout;

        public ReserveRequest(TimeSpan timeout)
        {
            _timeout = timeout;
        }

        public ReserveRequest()
        {
            _timeout = null;
        }

        public byte[] ToByteArray()
        {
            if (_timeout.HasValue)
                return "reserve-with-timeout {0}\r\n".FormatWith((int)_timeout.Value.TotalSeconds).ToASCIIByteArray();
            else
                return "reserve\r\n".ToASCIIByteArray();
        }

        public void Process(string firstLine, NetworkStream stream)
        {
            var parts = firstLine.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            switch (parts[0])
            {
                case "RESERVED":
                    var id = Convert.ToInt32(parts[1]);
                    var bytes = Convert.ToInt32(parts[2]);
                    var descr = GetJobFromBuffer(id, stream, bytes);
                    _tcs.SetResult(descr);
                    return;

                case "TIMED_OUT":
                    _tcs.SetException(new TimeoutException("Timeout expired waiting to reserve a job"));
                    return;

                case "DEADLINE_SOON":
                    // TODO: WTF do I do with this? Reschedule?
                    _tcs.SetException(new Exception("Deadline soon"));
                    return;

                default:
                    _tcs.SetException(new Exception("Unknown failure"));
                    return;
            }
        }

        internal static Job GetJobFromBuffer(int id, NetworkStream stream, int bytes)
        {
            var buffer = new byte[bytes];
            var readBytes = stream.Read(buffer, 0, bytes);
            if (readBytes != bytes)
            {
                // TODO: Now what, genius?
            }
            stream.ReadByte(); // CR
            stream.ReadByte(); // LF

            return new Job
            {
                Id = id,
                Data = buffer,
            };
        }
    }
}