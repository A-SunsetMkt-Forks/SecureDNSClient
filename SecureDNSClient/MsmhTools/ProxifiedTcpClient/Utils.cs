using System;
using System.Text;
using System.Globalization;
using System.Net.Sockets;
using System.Net;
using System.ComponentModel;

namespace MsmhTools.ProxifiedTcpClient
{
    internal static class Utils
    {
        /// <summary>
        /// Encodes a byte array to a string in 2 character hex format.
        /// </summary>
        /// <param name="data">Array of bytes to convert.</param>
        /// <returns>String containing encoded bytes.</returns>
        /// <remarks>e.g. 0x55 ==> "55", also left pads with 0 so that 0x01 is "01" and not "1"</remarks>
        public static string HexEncode(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return HexEncode(data, false, data.Length);
        }

        /// <summary>
        /// Encodes a byte array to a string in 2 character hex format.
        /// </summary>
        /// <param name="data">Array of bytes to encode.</param>
        /// <param name="insertColonDelimiter">Insert colon as the delimiter between bytes.</param>
        /// <param name="length">Number of bytes to encode.</param>
        /// <returns>String containing encoded bytes.</returns>
        /// <remarks>e.g. 0x55 ==> "55", also left pads with 0 so that 0x01 is "01" and not "1"</remarks>
        public static string HexEncode(byte[] data, bool insertColonDelimiter, int length)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            StringBuilder buffer = new(length * 2);

            int len = data.Length;
            for (int i = 0; i < len; i++)
            {
                buffer.Append(data[i].ToString("x").PadLeft(2, '0')); //same as "%02X" in C
                if (insertColonDelimiter && i < len - 1)
                    buffer.Append(':');
            }
            return buffer.ToString();
        }

        internal static string GetHost(TcpClient client)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            string host = string.Empty;
            try
            {
                if (client.Client.RemoteEndPoint != null)
                    host = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            }
            catch (Exception)
            {
                // do nothing
            };

            return host;
        }

        internal static string GetPort(TcpClient client)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            string port = "";
            try
            {
                if (client.Client.RemoteEndPoint != null)
                    port = ((System.Net.IPEndPoint)client.Client.RemoteEndPoint).Port.ToString(CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                // do nothing
            };

            return port;
        }

    }

    /// <summary>
    /// Event arguments class for the EncryptAsyncCompleted event.
    /// </summary>
    public class CreateConnectionAsyncCompletedEventArgs : AsyncCompletedEventArgs
    {
        private TcpClient? _proxyConnection;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="error">Exception information generated by the event.</param>
        /// <param name="cancelled">Cancelled event flag. This flag is set to true if the event was cancelled.</param>
        /// <param name="proxyConnection">Proxy Connection. The initialized and open TcpClient proxy connection.</param>
        public CreateConnectionAsyncCompletedEventArgs(Exception? error, bool cancelled, TcpClient? proxyConnection) : base(error, cancelled, null)
        {
            _proxyConnection = proxyConnection;
        }

        /// <summary>
        /// The proxy connection.
        /// </summary>
        public TcpClient? ProxyConnection
        {
            get { return _proxyConnection; }
        }
    }
}
