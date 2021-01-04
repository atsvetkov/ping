using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ping
{
    class Program
    {
        private const int DefaultPayloadSize = 32;
        private const int DefaultRepetitionsCount = 4;
        private const int DefaultTimeoutInMilliseconds = 1000;

        private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

        static void Main(string[] args)
        {
            var host = args[0];
            try
            {
                var ip = GetIpAddress(host);
                var endpoint = new IPEndPoint(ip, 0);
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
                socket.SendTimeout = socket.ReceiveTimeout = DefaultTimeoutInMilliseconds;

                try
                {
                    socket.Connect(endpoint);
                    if (socket.Connected)
                    {
                        Console.WriteLine($"Pinging {host}{(host == ip.ToString() ? string.Empty : $" [{ip}]")} with {DefaultPayloadSize} bytes of data:");
                        var sw = new Stopwatch();
                        var data = Enumerable.Repeat((byte)'w', DefaultPayloadSize).ToArray();
                        for (ushort rep = 0; rep < DefaultRepetitionsCount; rep++)
                        {
                            // send ping request
                            var echoRequest = IcmpPacket.CreateEchoRequest(1, rep, data);

                            var reply = new byte[1024];

                            sw.Reset();
                            sw.Start();
                            socket.Send(echoRequest);

                            // receive ping reply
                            try
                            {
                                var bytesReceived = socket.Receive(reply);
                                sw.Stop();

                                var replyPayloadSize = bytesReceived - IcmpPacket.IpHeaderSize - IcmpPacket.IcmpHeaderSize;

                                // Time To Live value is in the 9th byte of the ping reply
                                var ttl = reply[8];

                                try
                                {
                                    var echoReply = IcmpPacket.FromBytes(reply);
                                    if (echoReply.Identifier == echoRequest.Identifier && echoReply.SequenceNumber == echoRequest.SequenceNumber)
                                    {
                                        Console.WriteLine($"Reply from {ip}: bytes={replyPayloadSize} time={sw.ElapsedMilliseconds}ms TTL={ttl}");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Reply from {ip}: wrong identifier or sequence number");
                                    }
                                }

                                catch(IcmpChecksumException)
                                {
                                    Console.WriteLine($"Reply from {ip}: INCORRECT CHECKSUM");
                                }
                            }
                            catch (SocketException)
                            {
                                Console.WriteLine($"Reply from {ip}: Destination host unreachable.");
                            }

                            // wait up to a second, so that the execution feels smooth
                            if (rep < DefaultRepetitionsCount-1 && sw.Elapsed < OneSecond)
                            {
                                Thread.Sleep(OneSecond - sw.Elapsed);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Ping request could not find host {host}.");
                    }
                }
                catch (SocketException)
                {
                    Console.WriteLine($"Ping could not connect to {host}.");
                }
            }
            catch (SocketException)
            {
                Console.WriteLine($"Ping request could not find host {host}. Please check the name and try again.");
            }
        }

        static IPAddress GetIpAddress(string host)
        {
            if (IPAddress.TryParse(host, out var address))
            {
                return address;
            }

            var entry = Dns.GetHostEntry(host);

            // for now - always ping using IPv4 protocol
            return Array.Find(entry.AddressList, i => i.AddressFamily == AddressFamily.InterNetwork);
        }
    }
}
