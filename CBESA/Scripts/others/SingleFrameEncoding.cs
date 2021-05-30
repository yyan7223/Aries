using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using SPINACH.Networking;
using SPINACH.Media;

public class SingleFrameEncoding : MonoBehaviour
{
    int TotalEncodedBytes = 0;
    public Camera cam;
    RenderTexture remoteRT; // Server
    List<float> scaleList;
    float remoteRTScale = 0.1f;
    VideoEncodingSession _remoteRTEncodingSession;
    Thread _remoteRTPipeThread;
    int texEncodeRate = 90;
    int frameCount = 0;

    GameObject borderScene; // borderScene is used for checking the border
    Renderer[] borderRenderers;
    float xMin, xMax, zMin, zMax;
    int xMoveCount, zMoveCount, yRotateCount;
    float xMoveStride, zMoveStride, yRotateStride;
    int xCount, zCount, yCount;
    float initialRotationX, initialRotationZ, initialPositionY;
    List<float> PerformanceDataset;

    // Start is called before the first frame update
    void Start()
    {
        PerformanceDataset = new List<float>();
        
        // Get bounds of the moving area
        Bounds b = new Bounds();
        borderScene = GameObject.Find("/Content/Buildings");
        borderRenderers = borderScene.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in borderRenderers)
        {
            b.Encapsulate(r.bounds); // refreshing bounds according each renderer
        }

        xMin = b.center.x - b.extents.x;
        xMax = b.center.x + b.extents.x;
        zMin = b.center.z - b.extents.z;
        zMax = b.center.z + b.extents.z;

        xMoveCount = 10;
        zMoveCount = 10;
        yRotateCount = 4;
        xMoveStride = (xMax - xMin) / xMoveCount;
        zMoveStride = (zMax - zMin) / zMoveCount;
        yRotateStride = 360 / yRotateCount;

        xCount = 0;
        zCount = 0;
        yCount = 0;

        initialPositionY = transform.position.y;
        initialRotationX = transform.rotation.x;
        initialRotationZ = transform.rotation.z;
    }

    // Update is called once per frame
    void Update()
    {
        // update transform
        transform.position = new Vector3(xMin + xCount * xMoveStride, initialPositionY, zMin + zCount * zMoveStride);
        transform.rotation = Quaternion.Euler(initialRotationX, 0.0f + yCount * yRotateStride, initialRotationZ);

        yCount++;
        if(yCount == yRotateCount)
        {
            yCount = 0;
            xCount++;
        }
        if(xCount == xMoveCount)
        {
            xCount = 0;
            zCount++;
        }
        if(zCount == zMoveCount)
        {
            saveTextFile();
            Application.Quit();
        }

        // refresh the rendering target according to new remotescale
        RefreshRenderTarget(remoteRTScale);
        PrepareEncodeService(remoteRTScale);
        cam.targetTexture = remoteRT;
        cam.enabled = false;

        remoteRT.Release();
        cam.Render();

        if(frameCount < xMoveCount * zMoveCount * yRotateCount)
        {
            Debug.Log(string.Format("Total encoded bytes amount are {0} MB", TotalEncodedBytes / 1024f / 1024f));
            PerformanceDataset.Add(TotalEncodedBytes / 1024f / 1024f);
        } 

        frameCount++;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        PushRenderedResult2FFmpeg();
        var s = _remoteRTEncodingSession.EndSession(); // waiting for the encoding for this frame finished
        _remoteRTPipeThread.Abort();
    }

    void RefreshRenderTarget(float remoteRTScale)
    {
        remoteRT = new RenderTexture(
            (int)(UnityEngine.Screen.width * remoteRTScale),
            (int)(UnityEngine.Screen.height * remoteRTScale),
            16, RenderTextureFormat.ARGB32
        );
    }

    // Fetch tbe rendered remoteRT from GPU, encode them to byte stream and send through the network
    public void PushRenderedResult2FFmpeg()
    {
        // Fetch the remotely rendered texture in GPU, store as the byte stream, and push it to the encoding session
        var request = AsyncGPUReadback.Request(remoteRT);
        request.WaitForCompletion();//bruh async fuck off.
        var requestTexture = request.GetData<byte>();
        byte[] managed = new byte[requestTexture.Length];
        requestTexture.CopyTo(managed);
        _remoteRTEncodingSession.PushFrame(managed); // push into the encoding stream 
    }

    // Start remoteRT encoding session
    private void PrepareEncodeService(float remoteRTScale)
    {
        _remoteRTEncodingSession = new VideoEncodingSession((int)(UnityEngine.Screen.width * remoteRTScale),
                                                            (int)(UnityEngine.Screen.height * remoteRTScale),
                                                            1600, 2000, texEncodeRate);
        
        _remoteRTPipeThread = new Thread(CheckTotalEncodedBytesAmount);
        _remoteRTPipeThread.Start();
    }

    // Thread function to send encoded RT (byte stream) to Client
    private void CheckTotalEncodedBytesAmount()
    {
        int act = 0;
        byte[] buf = new byte[5242880]; // read 5 MB data each time
        do
        {
            act = _remoteRTEncodingSession.ConsumeEncodedStream(buf, 0, buf.Length);

            if (act > 0)
            {
                TotalEncodedBytes += act;
            }
        } while (act != 0);
    }

    void saveTextFile()
    {
        // save results to txt file
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var item in PerformanceDataset)
        {
            sb.AppendLine(item.ToString());
        }

        Console.WriteLine(sb.ToString());
        if(Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(
                Application.dataPath, "SingleFrameEncoding.txt"), 
                sb.ToString());
        }
        else if(Application.platform == RuntimePlatform.Android)
        {
            // Go to your Player settings, for Android, Change Write access from "Internal Only" to External (SDCard). 
            // You can then use Application.persistentDataPath to get the location of your external storage path.
            // Application.persistentDataPath on android points to /storage/emulated/0/Android/data/<packagename>/files on most devices
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(
                Application.persistentDataPath, "SingleFrameEncoding.txt"),
                sb.ToString());
        }
        Console.ReadLine();
    }

}
