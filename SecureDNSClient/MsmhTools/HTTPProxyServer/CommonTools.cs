﻿using System;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace MsmhTools.HTTPProxyServer
{
    internal static class CommonTools
    {
        internal static async Task<bool> IsIpBlocked(string ip, int port, int timeoutMS)
        {
            bool canPing = await CanPing(ip, timeoutMS);
            
            if (canPing)
            {
                bool canTcpConnect;
                try
                {
                    canTcpConnect = await CanTcpConnect(ip, port, timeoutMS);
                }
                catch (Exception)
                {
                    canTcpConnect = false;
                }

                if (canTcpConnect)
                {
                    bool canConnect;
                    try
                    {
                        canConnect = await CanConnect(false, ip, port, timeoutMS);
                    }
                    catch (Exception)
                    {
                        canConnect = false;
                    }

                    return !canConnect;
                }
                else
                    return true;
            }
            else
                return true;
        }

        internal static async Task<bool> IsHostBlocked(string host, int port, int timeoutMS)
        {
            IPAddress? ip = HostToIP(host);
            if (ip != null)
            {
                return await IsIpBlocked(ip.ToString(), port, timeoutMS);
            }
            else
                return true;
        }

        internal static async Task<bool> IsHostBlockedBySNI(string host, int port, int timeoutMS)
        {
            bool canPing = await CanPing(host, timeoutMS);

            if (canPing)
            {
                bool canConnect;
                try
                {
                    canConnect = await CanConnect(true, host, port, timeoutMS);
                }
                catch (Exception)
                {
                    canConnect = false;
                }

                return !canConnect;
            }
            else
                return true;
        }

        private static async Task<bool> CanPing(string host, int timeoutMS)
        {
            var task = Task.Run(() =>
            {
                try
                {
                    Ping ping = new();
                    PingReply reply = ping.Send(host, timeoutMS);
                    if (reply == null) return false;

                    return reply.Status == IPStatus.Success;
                }
                catch (PingException)
                {
                    return false;
                }
            });

            if (await task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMS + 500)))
                return task.Result;
            else
                return false;
        }

        private static async Task<bool> CanTcpConnect(string host, int port, int timeoutMS)
        {
            var task = Task.Run(() =>
            {
                try
                {
                    using TcpClient client = new(host, port);
                    client.SendTimeout = timeoutMS;
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            });

            if (await task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMS + 500)))
                return task.Result;
            else
                return false;
        }

        private static async Task<bool> CanConnect(bool https, string host, int port, int timeoutMS)
        {
            var task = Task.Run(async () =>
            {
                try
                {
                    string url;
                    if (https) url = $"https://{host}:{port}";
                    else url = $"http://{host}";

                    Uri uri = new(url, UriKind.Absolute);

                    using HttpClient httpClient = new();
                    httpClient.Timeout = TimeSpan.FromMilliseconds(timeoutMS);

                    await httpClient.GetAsync(uri);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            });

            if (await task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMS + 500)))
                return task.Result;
            else
                return false;
        }

        internal static IPAddress? HostToIP(string host, bool getIPv6 = false)
        {
            IPAddress? result = null;

            try
            {
                //IPAddress[] ipAddresses = Dns.GetHostEntry(host).AddressList;
                IPAddress[] ipAddresses = Dns.GetHostAddresses(host);

                if (ipAddresses == null || ipAddresses.Length == 0)
                    return null;

                if (!getIPv6)
                {
                    for (int n = 0; n < ipAddresses.Length; n++)
                    {
                        var addressFamily = ipAddresses[n].AddressFamily;
                        if (addressFamily == AddressFamily.InterNetwork)
                        {
                            result = ipAddresses[n];
                            break;
                        }
                    }
                }
                else
                {
                    for (int n = 0; n < ipAddresses.Length; n++)
                    {
                        var addressFamily = ipAddresses[n].AddressFamily;
                        if (addressFamily == AddressFamily.InterNetworkV6)
                        {
                            result = ipAddresses[n];
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            return result;
        }

        internal static byte[] AppendBytes(byte[] orig, byte[] append)
        {
            if (append == null) return orig;
            if (orig == null) return append;

            byte[] ret = new byte[orig.Length + append.Length];
            Buffer.BlockCopy(orig, 0, ret, 0, orig.Length);
            Buffer.BlockCopy(append, 0, ret, orig.Length, append.Length);
            return ret;
        }

        /// <summary>
        /// Fully read a stream into a byte array.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <returns>A byte array containing the data read from the stream.</returns>
        internal static byte[] StreamToBytes(Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new InvalidOperationException("Input stream is not readable");

            byte[] buffer = new byte[16 * 1024];
            using MemoryStream ms = new();
            int read;

            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, read);
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Add a key-value pair to a supplied Dictionary.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">The value.</param>
        /// <param name="existing">An existing dictionary.</param>
        /// <returns>The existing dictionary with a new key and value, or, a new dictionary with the new key value pair.</returns>
        internal static Dictionary<string, string> AddToDict(string key, string? val, Dictionary<string, string> existing)
        {
            if (string.IsNullOrEmpty(key)) return existing;

            Dictionary<string, string> ret = new();

            if (existing == null)
            {
                ret.Add(key, val);
                return ret;
            }
            else
            {
                if (existing.ContainsKey(key))
                {
                    if (string.IsNullOrEmpty(val)) return existing;
                    string tempVal = existing[key];
                    tempVal += "," + val;
                    existing.Remove(key);
                    existing.Add(key, tempVal);
                    return existing;
                }
                else
                {
                    existing.Add(key, val);
                    return existing;
                }
            }
        }

        internal static bool IsWin7()
        {
            bool result = false;
            OperatingSystem os = Environment.OSVersion;
            Version vs = os.Version;

            if (os.Platform == PlatformID.Win32NT)
            {
                if (vs.Minor == 1 && vs.Major == 6)
                    result = true;
            }

            return result;
        }

        internal static byte[]? ReadHeaders(TcpClient client)
        {
            byte[]? headerBytes = null;
            byte[] lastFourBytes = new byte[4];
            lastFourBytes[0] = 0x00;
            lastFourBytes[1] = 0x00;
            lastFourBytes[2] = 0x00;
            lastFourBytes[3] = 0x00;

            // Attach-Stream
            NetworkStream stream = client.GetStream();

            if (!stream.CanRead)
            {
                throw new IOException("Unable to read from stream.");
            }

            // Read-Headers
            using (MemoryStream headerMs = new())
            {
                // Read-Header-Bytes
                byte[] headerBuffer = new byte[1];
                int read = 0;
                int headerBytesRead = 0;

                while ((read = stream.Read(headerBuffer, 0, headerBuffer.Length)) > 0)
                {
                    if (read > 0)
                    {
                        // Initialize-Header-Bytes-if-Needed
                        headerBytesRead += read;
                        if (headerBytes == null) headerBytes = new byte[1];

                        // Update-Last-Four
                        if (read == 1)
                        {
                            lastFourBytes[0] = lastFourBytes[1];
                            lastFourBytes[1] = lastFourBytes[2];
                            lastFourBytes[2] = lastFourBytes[3];
                            lastFourBytes[3] = headerBuffer[0];
                        }

                        // Append-to-Header-Buffer
                        byte[] tempHeader = new byte[headerBytes.Length + 1];
                        Buffer.BlockCopy(headerBytes, 0, tempHeader, 0, headerBytes.Length);
                        tempHeader[headerBytes.Length] = headerBuffer[0];
                        headerBytes = tempHeader;

                        // Check-for-End-of-Headers
                        if ((int)(lastFourBytes[0]) == 13
                            && (int)(lastFourBytes[1]) == 10
                            && (int)(lastFourBytes[2]) == 13
                            && (int)(lastFourBytes[3]) == 10)
                        {
                            break;
                        }
                    }
                }
            }

            // Process-Headers
            if (headerBytes == null || headerBytes.Length < 1)
            {
                //throw new IOException("No header data read from the stream.");
                return null;
            }
            return headerBytes;
        }

        internal static int Search(byte[] src, byte[] pattern)
        {
            int maxFirstCharSlot = src.Length - pattern.Length + 1;
            for (int i = 0; i < maxFirstCharSlot; i++)
            {
                if (src[i] != pattern[0]) // compare only first byte
                    continue;

                // found a match on first byte, now try to match rest of the pattern
                for (int j = pattern.Length - 1; j >= 1; j--)
                {
                    if (src[i + j] != pattern[j]) break;
                    if (j == 1) return i;
                }
            }
            return -1;
        }

    }
}
