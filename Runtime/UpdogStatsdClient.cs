using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Updog.Unity
{
    internal sealed class UpdogStatsdClient : IDisposable
    {
        private readonly UdpClient client;
        private readonly IPEndPoint endpoint;

        public UpdogStatsdClient(string address)
        {
            var separator = address.LastIndexOf(':');
            if (separator <= 0 || !int.TryParse(address.Substring(separator + 1), out var port))
            {
                throw new ArgumentException("StatsD endpoint must use host:port format", nameof(address));
            }

            var host = address.Substring(0, separator);
            var addresses = Dns.GetHostAddresses(host);
            if (addresses.Length == 0)
            {
                throw new ArgumentException("StatsD host could not be resolved", nameof(address));
            }

            var selectedAddress = addresses[0];
            foreach (var candidate in addresses)
            {
                if (candidate.AddressFamily == AddressFamily.InterNetwork)
                {
                    selectedAddress = candidate;
                    break;
                }
            }

            endpoint = new IPEndPoint(selectedAddress, port);
            client = new UdpClient(endpoint.AddressFamily);
        }

        public bool Send(UpdogMetric metric)
        {
            try
            {
                var payload = Encoding.UTF8.GetBytes(metric.ToStatsd());
                return client.Send(payload, payload.Length, endpoint) == payload.Length;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            client?.Dispose();
        }
    }
}
