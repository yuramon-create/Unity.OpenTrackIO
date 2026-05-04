using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace OpenTrackIO
{
    public class OpenTrackIOServer : MonoBehaviour
    {
        private static readonly Dictionary<int, OpenTrackIOServer> servers = new Dictionary<int, OpenTrackIOServer>();

        public static OpenTrackIOServer Get(int port)
        {
            if (!servers.TryGetValue(port, out var server))
            {
                server = new OpenTrackIOServer();
                server.listenPort = port;
                server.active = true;
                Task.Run(server.Start);
                servers.Add(port, server);
            }
            return server;
        }

        public int listenPort = 40_000;
        public delegate void PacketReceived(Packet packet);
        public PacketReceived received;
        private bool active = true;

        public async void Start()
        {
            using var listener = new UdpClient(listenPort);
            try
            {
#if UNITY_EDITOR
                Debug.Log($"OpenTrackIO server started listening on port {listenPort}");
#endif
                while (active)
                {
                    var result = await listener.ReceiveAsync();
                    try
                    {
                        var packet = Packet.Decode(result.Buffer);
                        if (received != null)
                        {
                            received(packet);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                    }
                }
            }
            catch (SocketException e)
            {
                Debug.LogError(e);
            }
        }

        public void Stop()
        {
            active = false;
        }
    }
}
