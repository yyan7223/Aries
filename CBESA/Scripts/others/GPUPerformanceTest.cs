using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

public class GPUPerformanceTest : MonoBehaviour
{
    private FRutils _FRutils;
    public Camera cam;

    GameObject borderScene; // borderScene is used for checking the border
    Renderer[] borderRenderers;
    Renderer[] allRenderers;
    
    List<PerformanceData> PerformanceDataset;
    Vector2 foveaCoordinate = new Vector2(0.5f, 0.5f);
    float E1 = 0.1f;
    float xMin, xMax, zMin, zMax;
    int xMoveCount, zMoveCount, yRotateCount;
    float xMoveStride, zMoveStride, yRotateStride;
    int xCount, zCount, yCount;
    float initialRotationX, initialRotationZ, initialPositionY;
    float latency;
    int frameCount = 0;

    // Unity profiler
    ProfilerRecorder setPassCallsRecorder;
    ProfilerRecorder drawCallsRecorder;
    ProfilerRecorder batchesRecorder;
    ProfilerRecorder verticesRecorder;
    ProfilerRecorder trianglesRecorder;
    ProfilerRecorder GfxUsedMemoryRecorder;

    public class PerformanceData
    {
        public PerformanceData()
        {
        }
        public long setPassCall { get; set; }
        public long drawCall { get; set; }
        public long batches { get; set; }
        public long vertices { get; set; }
        public long triangles { get; set; }
        public float latencyMilliSecond { get; set; }
        public Vector3 position { get; set; }
        public Vector3 rotation { get; set; }
        public float GfxMemory { get; set; }
        public override string ToString()
        {
            return 
            this.setPassCall.ToString() + "                         " 
            + this.drawCall.ToString() + "                         " 
            + this.batches.ToString() + "                         " 
            + this.vertices.ToString() + "                         " 
            + this.triangles.ToString() + "                         " 
            + this.position.ToString() + "                         " 
            + this.rotation.ToString() + "                         " 
            + this.GfxMemory.ToString() + "                         " 
            + this.latencyMilliSecond.ToString("F4");
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        _FRutils = new FRutils(cam); // initialize Foveated Rendering utils
        // cam.enabled = false;

        Application.targetFrameRate = 90;

        PerformanceDataset = new List<PerformanceData>();

        allRenderers = GameObject.Find("/Content").GetComponentsInChildren<Renderer>();
        
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

        xMoveCount = 30;
        zMoveCount = 30;
        yRotateCount = 20;
        xMoveStride = (xMax - xMin) / xMoveCount;
        zMoveStride = (zMax - zMin) / zMoveCount;
        yRotateStride = 360 / yRotateCount;

        xCount = 0;
        zCount = 0;
        yCount = 0;

        initialPositionY = transform.position.y;
        initialRotationX = transform.rotation.x;
        initialRotationZ = transform.rotation.z;

        // Start profiling
        setPassCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        batchesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        verticesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
        trianglesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        GfxUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");

        // Add custom code before Unity renders an individual Camera.
        // RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    // Update is called once per frame
    void Update()
    {
        if(frameCount > 0 && frameCount < xMoveCount * zMoveCount * yRotateCount + 1)
        {
            latency = Time.deltaTime;
            PerformanceDataset.Add(new PerformanceData{setPassCall = setPassCallsRecorder.LastValue,
                                                        drawCall = drawCallsRecorder.LastValue,
                                                        batches = batchesRecorder.LastValue,
                                                        vertices = verticesRecorder.LastValue,
                                                        triangles = trianglesRecorder.LastValue,
                                                        position = transform.position,
                                                        rotation = transform.rotation.eulerAngles,
                                                        GfxMemory = GfxUsedMemoryRecorder.LastValue / 1024f / 1024f,
                                                        latencyMilliSecond = latency * 1000});
        }
        
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

        frameCount++;
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
                Application.dataPath, "GPUPerformance.txt"), 
                sb.ToString());
        }
        else if(Application.platform == RuntimePlatform.Android)
        {
            // Go to your Player settings, for Android, Change Write access from "Internal Only" to External (SDCard). 
            // You can then use Application.persistentDataPath to get the location of your external storage path.
            // Application.persistentDataPath on android points to /storage/emulated/0/Android/data/<packagename>/files on most devices
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(
                Application.persistentDataPath, "GPUPerformance.txt"),
                sb.ToString());
        }
        Console.ReadLine();
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        // Override cullingMatrix according to newly calculated PM before performing the culling operation.
        // https://docs.unity3d.com/ScriptReference/Camera-cullingMatrix.html

        E1 = 0.5f;
        while(E1 > 0.01f)
        {
            camera.ResetProjectionMatrix();
            _FRutils.RefreshPM(camera, foveaCoordinate, E1);
            camera.cullingMatrix = camera.projectionMatrix * camera.worldToCameraMatrix; 

            // Get the culling parameters from the current Camera
            camera.TryGetCullingParameters(out var cullingParameters);

            // Use the culling parameters to perform a cull operation, and store the results
            var cullingResults = context.Cull(ref cullingParameters);

            // var visibleObjectsAmount = checkVisibleObjects();

            E1 -= 0.05f;
        }     

        // Debug.Log("XX");
    }

    private int checkVisibleObjects()
    {
        int visibleObjectsAmount = 0;
        foreach(var renderer in allRenderers)
        {
           if(renderer.isVisible) visibleObjectsAmount++;
        }
        return visibleObjectsAmount;
    }

    private void OnDisable()
    {
        // Remove WriteLogMessage as a delegate of the  RenderPipelineManager.beginCameraRendering event
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }
    
}
