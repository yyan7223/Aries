using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

public class QBESA : MonoBehaviour
{
    private FRutils _FRutils;
    public Camera cam;
    private float ScreenRefreshRate = 60;

    List<PerformanceData> PerformanceDataset;
    Vector2 foveaCoordinate = new Vector2(0.5f, 0.5f);
    float E1 = 0.05f;
    float lastFrameEcc = 0.05f;

    // check gameobjects visibility related parameters
    // The three array below are in the same length, they contained the info all the gameobjects who have renderers componnet, 
    Renderer[] Renderers; 
    int[] Triangles;
    Vector3[] Positions;

    // check border related parameters
    GameObject borderScene; // used for checking the border
    Renderer[] borderRenderers;
    Bounds b;
    float xMin, xMax, zMin, zMax;
    int xMoveCount, zMoveCount, yRotateCount;
    float xMoveStride, zMoveStride, yRotateStride;
    int xCount, zCount, yCount;
    float referenceDist; 
    
    float initialRotationX, initialRotationZ, initialPositionY;
    float lastFrameLatency = 0; // the rendering time of last frame
    bool isSuccessfullyControlled = false; // indicate whether complexity are successfully controlled under MAC
    int frameCount = 0;

    // scene complexity related parameters
    float newFrameComplexity_CPU = 0;
    float newFrameComplexity_GPU = 0;
    float oldFrameComplexity_CPU = 0;
    float oldFrameComplexity_GPU = 0;

    private LIWCutils _LIWCutils; 
    Vector3 oldPos, newPos;
    Quaternion oldRot, newRot;
    Vector2 oldCoord, newCoord;
    uint mappingIndex;

    DateTime beforeDT;
    DateTime afterDT;
    TimeSpan ts;

    public class PerformanceData
    {
        public PerformanceData()
        {
        }
        public float estimatedComplexity_CPU { get; set; }
        public float estimatedComplexity_GPU { get; set; }
        public float latencyMilliSecond { get; set; }
        public float eccentricity { get; set; }
        public double executionTime { get; set; }
        public override string ToString()
        {
            return this.estimatedComplexity_CPU.ToString("F4") + "                         " 
            + this.estimatedComplexity_GPU.ToString("F4") + "                         " 
            + this.latencyMilliSecond.ToString("F4") + "                         " 
            + this.eccentricity.ToString("F4") + "                         " 
            + this.executionTime.ToString("F4");
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        _FRutils = new FRutils(cam); 
        Application.targetFrameRate = 90;
        PerformanceDataset = new List<PerformanceData>();

        GameObject scene = GameObject.Find("/Content");
        _LIWCutils = new LIWCutils(scene);

        // For current whole scene, record triangles and position of each gameobject who has Renderer components, no matter whether they are visible to camera
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

        // Get bounds of the moving area
        b = new Bounds();
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

        xMoveCount = 30; // agressive path
        // xMoveCount = 1; // conservative path
        yRotateCount = 20; // agressive path
        // yRotateCount = 180; // conservative path
        zMoveCount = 30;
        xMoveStride = (xMax - xMin) / xMoveCount;
        zMoveStride = (zMax - zMin) / zMoveCount;
        yRotateStride = 360 / yRotateCount;

        xCount = 0;
        zCount = 0;
        yCount = 0;

        initialPositionY = transform.position.y;
        initialRotationX = transform.rotation.x;
        initialRotationZ = transform.rotation.z;

        // referenceDist = (float)(Math.Sqrt(Math.Pow (2 * b.extents.x, 2) + Math.Pow (2 * b.extents.z, 2))); // The largest real world distance
        referenceDist = 1000; 

        // Add custom code before Unity renders an individual Camera.
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        // update position and rotation
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
            saveTextFile();
            Application.Quit(); // conservative path
            xCount = 0;
            zCount++;
        }
        if(zCount == zMoveCount)
        {
            saveTextFile();
            Application.Quit(); // agressive path
        }
        frameCount++;

        /********************************************* QSEA *************************************************/
        // The latency of frame N-1 can be accessed at the beginning of frame N
        lastFrameLatency = Time.deltaTime;

        // record the data
        if(frameCount > 0 && frameCount < xMoveCount * zMoveCount * yRotateCount + 1)
        {
            PerformanceDataset.Add(new PerformanceData{estimatedComplexity_CPU = oldFrameComplexity_CPU,
                                                        estimatedComplexity_GPU = oldFrameComplexity_GPU,
                                                        latencyMilliSecond = lastFrameLatency * 1000,
                                                        eccentricity = lastFrameEcc,
                                                        executionTime = ts.TotalMilliseconds});  // store all the data of last frame                      
        }

        // The simulation of fetching fovea coordinate from eyetracker.
        foveaCoordinate = new Vector2(0.5f, 0.5f);

        if(frameCount > 0)
        {
            beforeDT = System.DateTime.Now;

            // Generate mapping index according to the old value and new value
            newPos = transform.root.position;
            newRot = transform.root.rotation;
            newCoord = foveaCoordinate;

            // caculate newFrameComplexity_CPU and newFrameComplexity_GPU
            var filteredIndex = CheckVisibility(cam, Positions); // get the index of visible gameObjects
            CalculateFrameComplexity(cam, filteredIndex); 

            // generating motion index
            mappingIndex = _LIWCutils.generateMappingIndex(oldPos, newPos, oldRot, newRot, oldCoord, newCoord,
                                                            newFrameComplexity_CPU, oldFrameComplexity_CPU,
                                                            newFrameComplexity_GPU, oldFrameComplexity_GPU);
            
            // get best E1 and update the Table
            E1 = _LIWCutils.selectBestEccentricity(frameCount, mappingIndex, lastFrameEcc, lastFrameLatency);

            afterDT = System.DateTime.Now;
            ts = afterDT.Subtract(beforeDT);
        }

        // Reset Camera PM according to best E1 
        cam.ResetProjectionMatrix();
        _FRutils.RefreshPM(cam, foveaCoordinate, E1);
        cam.cullingMatrix = cam.projectionMatrix * cam.worldToCameraMatrix; 

        // refresh the data to current frame after recording data
        lastFrameEcc = E1;
        oldFrameComplexity_CPU = newFrameComplexity_CPU;
        oldFrameComplexity_GPU = newFrameComplexity_GPU;  

        // let current Transform and eyes coordinates to be old values
        oldPos = transform.root.position;
        oldRot = transform.root.rotation;
        oldCoord = foveaCoordinate;
    }

    private void OnDisable()
    {
        // Remove WriteLogMessage as a delegate of the  RenderPipelineManager.beginCameraRendering event
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
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
        newFrameComplexity_CPU = 0; // clear former data
        newFrameComplexity_GPU = 0;
        
        foreach (var index in filteredIndex)
        {
            var Tris = Triangles[index];
            var distance = Vector3.Distance(Positions[index], cam.transform.position);
            newFrameComplexity_GPU += Tris * (1 - distance / referenceDist);
        }

        newFrameComplexity_CPU = filteredIndex.Count;
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
}
