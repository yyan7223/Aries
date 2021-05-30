using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SPINACH.Networking;
using SPINACH.Media;

namespace VPRAssets.Scripts
{
    public class NetworkMenu : MonoBehaviour
    {
        public NetworkDispatch dispatch;

        public Transform spawningPoint;
        public Transform spawningPoint2;

        public PeerPacketChannel peer;

        public bool server = false;

        public void StartServer()
        {
            Debug.Log("enter into StartServer() function");
            // Application.targetFrameRate = 30;
            server = true;
            dispatch._newPeerConnected += channel =>
            {
                peer = channel;
                // MakeLocalPlayer();
            };
            dispatch.EnableObjectRouter(true);
            // dispatch.StartAcceptingPeers(new TcpTransportListener(23563));
            dispatch.StartAcceptingPeers(new TcpTransportListener(23563));

        }

        public void StartClient()
        {
            Debug.Log("enter into StartClient() function");
            Application.targetFrameRate = 90;
            dispatch._onServerConnectionEstablished += () => { MakeLocalPlayer(); };
            dispatch.EnableObjectRouter(false);
            // peer = dispatch.EstablishConnection("127.0.0.1", 23563, NetworkTransportProtocol.TCP);
            peer = dispatch.EstablishConnection("172.16.0.105", 23563, NetworkTransportProtocol.TCP);
        }

        void MakeLocalPlayer()
        {
            var obj = dispatch.NetworkInstantiate(0, server ? spawningPoint.position : spawningPoint2.position, Quaternion.identity);
        }

    }
}
