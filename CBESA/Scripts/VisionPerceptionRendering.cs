using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using SPINACH.Networking;
using SPINACH.Media;

namespace VPRAssets.Scripts
{
    public class VisionPerceptionRendering : MonoBehaviour
    {
        // Foveated Rendering related parameters
        private FRutils _FRutils;
        private float E1 = 0.1f; // eccentricity ratio
        private float E2;
        private Vector2 foveaCoordinate = new Vector2(0.5f, 0.5f); // Define the coordinates where eyes focus on (normalized)
        private Vector3 eyeballMovingSpeed = new Vector3(0.1f, 0.1f, 0.1f);

        // Collaborative Foveated Rendering related parameters
        private Camera cam_remote;
        private RenderTexture remoteRT; // Server
        private Texture2D receivedRemoteRT; //Client
        private float remoteRTScale = 0.25f;

        private Camera cam_fovea;
        private RenderTexture foveaRT;
        private float foveaRTScale = 0.2f; // foveaRTScale should always be twice of E1

        public Material compositeMaterial; // material for shader that composite three render textures

        // ffmpeg encoding decoding related parameters
        private VideoEncodingSession _remoteRTEncodingSession; // ffmpeg encoding decoding related parameters
        private VideoDecodingSession _remoteRTDecodingSession;
        private NetworkObjectMessenger _nom;
        private Thread _remoteRTPipeThread;
        private Queue<VideoStreamSlicePacket> receivedRemoteRTSlices;
        private bool oneNewRemoteRT = false;
        private int texEncodeRate = 60;
    
        // LIWC component related parameters
        private LIWCutils _LIWCutils; 
        float latency = 0; // frame latency
        int frameCount = 0;
        Vector3 oldPos, newPos;
        Quaternion oldRot, newRot;
        Vector2 oldCoord, newCoord;
        uint mappingIndex;

        // RTBESA related parameters
        float frameComplexity_CPU = 0;
        float frameComplexity_GPU = 0;
        float lastFrameComplexity_CPU = 0;
        float lastFrameComplexity_GPU = 0;
        float lastExtraLatency = 0;
        float MAC_CPU = 40; // Kirin970 6; Snapdragon 40
        float MAC_GPU = 1520; // Kirin970 1520; Snapdragon 40
        float tuned_MAC_CPU = 40;
        float tuned_MAC_GPU = 1520;
        bool isSuccessfullyControlled = false;
        private Text screenText;
        private float ScreenRefreshRate = 60;
        float referenceDist = 1000; 

        // check gameobjects visibility related parameters
        // The three array below are in the same length, they contained the info all the gameobjects who have renderers componnet, 
        Renderer[] Renderers; 
        int[] Triangles;
        Vector3[] Positions;

        // Rendertexture transmission packet
        class VideoStreamSlicePacket : IRoutablePacketContent
        {
            public const byte NTYPE = 0xce;
            public const int MAXSLICE = 16384; 

            public byte[] codingBuf = new byte[MAXSLICE];
            public int validSize;

            public VideoStreamSlicePacket(byte[] buf, int offset, int len)
            {
                if (len > MAXSLICE)
                    throw new ArgumentOutOfRangeException("remote rendertexture max slice exceeded, manual slicing in needed.");

                Buffer.BlockCopy(buf, offset, codingBuf, 0, len);
                validSize = len;
            }

            public VideoStreamSlicePacket(byte[] bytes)
            {
                validSize = BitConverter.ToInt32(bytes, 0);
                Buffer.BlockCopy(bytes, 4, codingBuf, 0, validSize);
            }

            public byte GetNType()
            {
                return NTYPE;
            }

            public byte GetRevision()
            {
                return 0;
            }

            public int GetByteLength()
            {
                return MAXSLICE + 4;
            }

            public byte[] GetByteStream()
            {
                var buf = new byte[GetByteLength()];
                Buffer.BlockCopy(BitConverter.GetBytes(validSize), 0, buf, 0, 4);
                Buffer.BlockCopy(codingBuf, 0, buf, 4, validSize);
                return buf;
            }
        }

        void Awake()
        {
            _nom = transform.root.GetComponent<NetworkObjectMessenger>();
            if (NetworkDispatch.Default().isServer) PrepareEncodeService();
            if (!NetworkDispatch.Default().isServer) PrepareDecodeService();
        }

        void Start()
        {
            InitializeRenderTarget();

            if (NetworkDispatch.Default().isServer) // On server, disable Camera_fovea, get Camera_remote
            {
                transform.Find("Camera_fovea").gameObject.SetActive(false);

                cam_remote = transform.Find("Camera_remote").gameObject.GetComponent<Camera>();
                cam_remote.targetTexture = remoteRT;
                _FRutils = new FRutils(cam_remote); // initialize Foveated Rendering utils
                cam_remote.enabled = false;
            }

            if (!NetworkDispatch.Default().isServer) 
            { 
                // initialize LIWC component utils
                GameObject scene = GameObject.Find("/Content");
                _LIWCutils = new LIWCutils(scene);

                // On client, disable Camera_remote, get Camera_fovea 
                transform.Find("Camera_remote").gameObject.SetActive(false);

                cam_fovea = transform.Find("Camera_fovea").gameObject.GetComponent<Camera>();
                cam_fovea.targetTexture = foveaRT;
                _FRutils = new FRutils(cam_fovea); // initialize Foveated Rendering utils
                cam_fovea.enabled = false;

                screenText = GameObject.Find("Canvas").GetComponent<Text>();

                // For current whole scene, record triangles and position of each gameobject who has Renderer components, no matter whether they are visible to camera
                Debug.Log("Prefetching of triangles...");
                Renderers = GameObject.Find("/Content").GetComponentsInChildren<Renderer>();
                Triangles = new int[Renderers.Length];
                Positions = new Vector3[Renderers.Length];
                int index = 0;
                foreach (Renderer r in Renderers)
                {
                    try 
                    {
                        Triangles[index] = r.gameObject.GetComponent<MeshFilter>().mesh.triangles.Length / 3; // get corresponding triangles amount of each mesh
                    }
                    catch (Exception e)
                    {
                        Triangles[index] = 0;
                    }

                    Positions[index] = r.gameObject.transform.position; //The position in the world space is automatically returned, but this result will be diffrenet from the value shows on the inspector if current gameobject parent transform is not (0,0,0)
                    index++;
                }
            }
        }

        void Update()
        {
            // Debug.Log("Update");
            if (NetworkDispatch.Default().isServer)
            {
                // Perform rendering
                remoteRT.Release();
                cam_remote.Render();
                
                frameCount++;
            }

            if (!NetworkDispatch.Default().isServer)
            {
                // // // Refresh GPU_m and throughput according to frame time consumption
                // latency = Time.deltaTime;
                // Debug.Log(string.Format("The real-time FPS is: {0}", 1.0f/latency));
                // _LIWCutils.refreshGPUm(latency);
                // _LIWCutils.refreshThroughPut(latency);

                // // The simulation of fetching fovea coordinate from eyetracker.
                foveaCoordinate = new Vector2(0.5f, 0.5f);

                // if(frameCount > 0)
                // {
                //     // Generate mapping index according to the old value and new value
                //     newPos = transform.root.position;
                //     newRot = transform.root.rotation;
                //     newCoord = foveaCoordinate;
                //     mappingIndex = _LIWCutils.generateMappingIndex(oldPos, newPos, oldRot, newRot, oldCoord, newCoord);
                    
                //     // get best E1
                //     // _LIWCutils.totalVertsCounter(cam_fovea, E1, foveaCoordinate);
                //     // E1 = _LIWCutils.selectBestEccentricity(frameCount, mappingIndex, E1, latency);
                //     // Debug.Log(string.Format("frame: {0}, E1: {1}, vertice: {2}", frameCount, E1, _LIWCutils.totalVerticesCount));
                // }

                // RunTimeBestEccSelect(false);
                
                // // refresh the resolution of different layers according to the selected E1 and E2
                // foveaRTScale = 2 * E1;
                // RefreshRenderTextureScale(foveaRTScale);

                // Reset Camera PM according to best E1 and execute rendering
                cam_fovea.ResetProjectionMatrix();
                _FRutils.RefreshPM(cam_fovea, foveaCoordinate, E1);
                foveaRT.Release();
                cam_fovea.Render();

                // let current Transform and eyes coordinates to be old values
                // oldPos = transform.root.position;
                // oldRot = transform.root.rotation;
                // oldCoord = foveaCoordinate;

                frameCount++;
            }
        }


        /**
        * @description: OnRenderImage is automatically called after all rendering is complete to render image (LateUpdate).
                        Always used for Postprocessing effects.
                        It allows you to modify final image by processing it with shader based filters. 
                        The incoming image is source render texture.The result should end up in destination render texture.
                        You must always issue a Graphics.Blit() or render a fullscreen quad if your override this method.
        * @param {RenderTexture source, RenderTexture destination} 
        * @return {void} 
        */
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            // Debug.Log("VisionPerceptionRednereing OnRenderImage");
            // Both fovea camera and remote camera are attached below one single camera, therefore the onRenderImage() will be called for once after all Start() has been executed, before all Update()
            // we need to use 'frameCount != 0' to avoid unpredictable behaviour before entering into Update()
            if (NetworkDispatch.Default().isServer && frameCount != 0)
            {
                // Debug.Log("Server has finished rendering");
                EncodeRenderedFrame();
                Graphics.Blit(remoteRT, destination); // just to show what remoteRT looks like
                // if(PlayerControlLogic.clientMessageIsReceived) Debug.Log(string.Format("Server Send Time is {0}.{1}", DateTime.Now, DateTime.Now.Millisecond));
            }
            if (!NetworkDispatch.Default().isServer && frameCount != 0)
            {
                // keep waiting for remoteRT
                // while(true)
                // {
                //     if(_remoteRTDecodingSession.ConsumeFrame(receivedRemoteRT))
                //     {
                //         Debug.Log("Client has sucessfully receive remoteRT");
                //         break;
                //     }
                // }
                if(_remoteRTDecodingSession.ConsumeFrame(receivedRemoteRT)) Debug.Log(string.Format("successfully decode time is {0}.{1}", DateTime.Now, DateTime.Now.Millisecond));
                // _remoteRTDecodingSession.ConsumeFrame(receivedRemoteRT);

                _FRutils.RefreshShaderParameter(compositeMaterial, foveaRT,
                                                receivedRemoteRT,
                                                foveaCoordinate,
                                                E1, E1+0.1f);
                var startTime = Time.time;
                Graphics.Blit(receivedRemoteRT, destination, compositeMaterial);
                // Debug.Log(string.Format("reconstruction Time is {0}", Time.time - startTime));
            }
        }

        // Initialize rendertexture  
        void InitializeRenderTarget()
        {
            if (NetworkDispatch.Default().isServer)
            {
                remoteRT = new RenderTexture(
                    (int)(UnityEngine.Screen.width * remoteRTScale),
                    (int)(UnityEngine.Screen.height * remoteRTScale),
                    16, RenderTextureFormat.ARGB32
                );
            }
            if (!NetworkDispatch.Default().isServer)
            {
                // Creat the render texture
                foveaRT = new RenderTexture(
                    (int)(UnityEngine.Screen.width * foveaRTScale),
                    (int)(UnityEngine.Screen.height * foveaRTScale),
                    16, RenderTextureFormat.ARGB32
                );

                // fix the resolution
                receivedRemoteRT = new Texture2D(
                            (int)(UnityEngine.Screen.width * remoteRTScale),
                            (int)(UnityEngine.Screen.height * remoteRTScale),
                            TextureFormat.RGBA32, false);
                receivedRemoteRT.filterMode = FilterMode.Point;   
            }
        }

        // Change the resolution of foveaRT without reallocating it
        void RefreshRenderTextureScale(float scale)
        {
            // With 'Allow Dynamic Resolution' box checked, render targets have the DynamicallyScalable flag
            // The ScalableBufferManager handles the scaling of any render textures that have been marked to be DynamicallyScalable
            float widthScale = scale;
            float heightScale = scale;
            ScalableBufferManager.ResizeBuffers(widthScale, heightScale);
            Debug.Log(string.Format("frame: {0}, E1: {1}, foveaRT size: {2}x{3}:", frameCount, E1, foveaRT.width, foveaRT.height));
            // Please reference https://docs.unity3d.com/Manual/DynamicResolution.html
            // https://docs.unity3d.com/ScriptReference/ScalableBufferManager.html for more details
        }

        private void PrepareEncodeService()
        {
            _remoteRTEncodingSession = new VideoEncodingSession((int)(UnityEngine.Screen.width * remoteRTScale),
                                                            (int)(UnityEngine.Screen.height * remoteRTScale),
                                                            1600, 2000, texEncodeRate);

            _remoteRTPipeThread = new Thread(SendEncodedByteStream);
            _remoteRTPipeThread.Start();
            Debug.Log("remote rendertexture video encoder started!");
        }

        void SendEncodedByteStream()
        {
            while (true)
            {
                byte[] buf = new byte[VideoStreamSlicePacket.MAXSLICE];

                var act = _remoteRTEncodingSession.ConsumeEncodedStream(buf, 0, buf.Length);

                if (act > 0)
                {
                    _nom.SendMessage(new VideoStreamSlicePacket(buf, 0, act));

                }
            }
        }

        public void EncodeRenderedFrame()
        {
            // Fetch the remotely rendered texture in GPU, store as the byte stream, and push it to the encoding session
            var request = AsyncGPUReadback.Request(remoteRT);
            request.WaitForCompletion();//bruh async fuck off.
            var requestTexture = request.GetData<byte>();
            byte[] managed = new byte[requestTexture.Length];
            requestTexture.CopyTo(managed);
            _remoteRTEncodingSession.PushFrame(managed); // push into the encoding stream 
        }

        /**
         * @description: RegisterMethod to receive transmitted rendertexture, create decoding session
         * @param {void} 
         * @return {void} 
         */
        private void PrepareDecodeService()
        {
            receivedRemoteRTSlices = new Queue<VideoStreamSlicePacket>();
 
            _nom.RegisterMethod(VideoStreamSlicePacket.NTYPE, (rev, bytes) =>
              {
                  var p = new VideoStreamSlicePacket(bytes);
                  lock (receivedRemoteRTSlices)
                  {
                      receivedRemoteRTSlices.Enqueue(p);
                        // Debug.Log(string.Format("client received remote rendertexture {0}", p.validSize));
                  }
              });

            _remoteRTDecodingSession = new VideoDecodingSession((int)(UnityEngine.Screen.width * remoteRTScale),
                                                        (int)(UnityEngine.Screen.height * remoteRTScale));
            _remoteRTPipeThread = new Thread(FeedDecoder);
            _remoteRTPipeThread.Start();

            Debug.Log("remote rendertexture decoding services started and waiting for Remote rendertexture video stream!");
        }

        private void FeedDecoder()
        {
            while (true)
            {
                VideoStreamSlicePacket p = null;
                lock (receivedRemoteRTSlices)
                {
                    if (receivedRemoteRTSlices.Count > 0) p = receivedRemoteRTSlices.Dequeue();
                }
                if (p == null) continue;
                _remoteRTDecodingSession.PushStream(p.codingBuf, 0, p.validSize);
            }
        }

        void RunTimeBestEccSelect(bool finetune)
        {  
            // check last frame rendering time, don't move this code to other place
            var lastFrameLatency = Time.deltaTime; 
            screenText.text = string.Format("Real-Time FPS: {0:F4}", 1.0f/lastFrameLatency);

            if(finetune && frameCount > 0)
            {
                // if we have successfully controlled the last frame final frameComplexity_CPU and frameComplexity_GPU to be smaller than MAC
                // but the last frame rendering time is still larger than target FPS for example 16.66ms, 
                // it's time to slightly change the MAC
                if((lastFrameLatency > 1 / ScreenRefreshRate + 0.00333) && isSuccessfullyControlled)
                {
                    tuned_MAC_CPU = lastFrameComplexity_CPU;
                    tuned_MAC_GPU = lastFrameComplexity_GPU;
                }
                else // otherwise we recover it to initial MAC
                {
                    tuned_MAC_CPU = MAC_CPU;
                    tuned_MAC_GPU = MAC_GPU;
                }
            }

            // Best Ecc principle: when frameComplexity_CPU or frameComplexity_GPU larger than respective MAC, we keep executing the algorithm. 
            // This algorithm stop only after both frameComplexity_CPU and frameComplexity_GPU smaller then respective MAC or Ecc is close to zero.
            // E1, frameComplexity_CPU, and frameComplexity_GPU for current frame have been refreshed after executing this algorithm
            E1 = 0.55f;
            do
            {
                E1 -= 0.05f;
                cam_fovea.ResetProjectionMatrix();
                _FRutils.RefreshPM(cam_fovea, foveaCoordinate, E1);

                var filteredIndex = CheckVisibility(cam_fovea, Positions); // get the index of visible gameObjects
                CalculateFrameComplexity(cam_fovea, filteredIndex); // compute frame complexity under current eccentricity
            } while((frameComplexity_CPU > tuned_MAC_CPU || frameComplexity_GPU > tuned_MAC_GPU) && E1 > 0.05f);  

            // A bool variable which indicate whether current frame final frameComplexity_CPU and frameComplexity_GPU is smaller than MAC
            if(frameComplexity_CPU <= tuned_MAC_CPU && frameComplexity_GPU <= tuned_MAC_GPU) isSuccessfullyControlled = true;
            else isSuccessfullyControlled = false; // reset bool indicator
        }

        List<int> CheckVisibility(Camera cam, Vector3[] Positions)
        {
            List<int> filteredIndex = new List<int>();
            int index = 0;
            foreach (Vector3 pos in Positions)
            {
                var viewPos = cam.WorldToViewportPoint(pos);
                if(viewPos.z > 0 && viewPos.x >= 0 && viewPos.x <= 1 && viewPos.y >= 0 && viewPos.y <= 1) 
                {
                    filteredIndex.Add(index);
                }
                index++;
            }
            return filteredIndex;
        }

        void CalculateFrameComplexity(Camera cam, List<int> filteredIndex)
        {
            frameComplexity_CPU = 0; // clear former data
            frameComplexity_GPU = 0;
            
            foreach (var index in filteredIndex)
            {
                var Tris = Triangles[index];
                var distance = Vector3.Distance(Positions[index], cam.transform.position);
                frameComplexity_GPU += Tris * (1 - distance / referenceDist);
            }

            frameComplexity_CPU = filteredIndex.Count;
        }
    }
}