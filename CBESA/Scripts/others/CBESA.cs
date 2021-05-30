using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using System.Threading.Tasks;

public class CBESA : MonoBehaviour
{
    private FRutils _FRutils;
    public Camera cam;
    private float ScreenRefreshRate = 60;
    private float seachingInterval; // searching interval for CBESA
    private float seachingIntervalGT = 0.01f; // searching interval for ground truth

    List<PerformanceData> PerformanceDataset;
    Vector2 foveaCoordinate = new Vector2(0.5f, 0.5f);
    float E1 = 0.5f;
    float lastFrameEcc = 0.5f;

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
    int frameCount = 0;

    // scene complexity related parameters
    float frameComplexity_CPU = 0;
    float frameComplexity_GPU = 0;
    float lastFrameComplexity_CPU = 0;
    float lastFrameComplexity_GPU = 0;
    float extraLatency = 0;
    float lastExtraLatency = 0;
    float MAC_CPU = 6; // Kirin970 6; Snapdragon 40
    float MAC_GPU = 1520; // Kirin970 1520; Snapdragon 40

    // optimize eccentricity selection
    float MaximumGPUworkload = 0;
    float MinimumGPUWorkload = 0;
    float slope, bias; // workload = slope * Ecc + bias
    float MinEcc = 0.05f;
    float MaxEcc = 0.5f;

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
            // saveTextFile();
            // Application.Quit(); // conservative path
            xCount = 0;
            zCount++;
        }
        if(zCount == zMoveCount)
        {
            saveTextFile();
            Application.Quit(); // agressive path
        }
        frameCount++;

        /* run-time Best eccentricity selecting algorithm */
        DateTime beforeDT = System.DateTime.Now;
        RunTimeBestEccSelect(false);
        DateTime afterDT = System.DateTime.Now;
        TimeSpan ts = afterDT.Subtract(beforeDT);
        
        /* offline-baking MAC level */
        // OfflineBakingMAC();

        // record the data
        if(frameCount > 0 && frameCount < xMoveCount * zMoveCount * yRotateCount + 1)
        {
            PerformanceDataset.Add(new PerformanceData{estimatedComplexity_CPU = lastFrameComplexity_CPU,
                                                        estimatedComplexity_GPU = lastFrameComplexity_GPU,
                                                        latencyMilliSecond = lastFrameLatency * 1000 - lastExtraLatency,
                                                        eccentricity = lastFrameEcc,
                                                        executionTime = ts.TotalMilliseconds});  // store all the data of last frame                      
        }

        // refresh the data to current frame
        lastFrameEcc = E1;
        lastExtraLatency = extraLatency;
        lastFrameComplexity_CPU = frameComplexity_CPU;
        lastFrameComplexity_GPU = frameComplexity_GPU;  
    }

    private void OnDisable()
    {
        // Remove WriteLogMessage as a delegate of the  RenderPipelineManager.beginCameraRendering event
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    void RunTimeBestEccSelect(bool generateGroundTruth)
    {  
        // check last frame rendering time, don't move this code to other place
        lastFrameLatency = Time.deltaTime; 

        if(generateGroundTruth)
        {
            /************************** Searching for Ground Truth Eccentricity *******************************/
            E1 = 0.5f + seachingIntervalGT;
            do // exclusive searching, average 0.796 ms for each iteration
            {
                E1 -= seachingIntervalGT;
                LightweightRenderingWorkloadEstimationModule(cam, E1);
            } while((frameComplexity_CPU > MAC_CPU || frameComplexity_GPU > MAC_GPU) && E1 >= 0.06f); // minimum ecc is 0.05
            extraLatency = 0;   
            /**************************************************************************************************/
        }
        else
        {
            /************************** Conditional Best Eccentricity Selecting Algorithm *******************************/
            // firstly get the GPUworkload under full eccentricity and the minimum eccentricity
            LightweightRenderingWorkloadEstimationModule(cam, MaxEcc);
            MaximumGPUworkload = frameComplexity_GPU;

            LightweightRenderingWorkloadEstimationModule(cam, MinEcc);
            MinimumGPUWorkload = frameComplexity_GPU;

            if(MinimumGPUWorkload > MAC_GPU) E1 = 0.05f; // too complex
            else if(MaximumGPUworkload < MAC_GPU) E1 = 0.5f; // too simple
            else
            {
                E1 = 0.5f; 
                seachingInterval = (MaxEcc - MinEcc) * 0.5f;
                do 
                {
                    E1 -= seachingInterval;
                    LightweightRenderingWorkloadEstimationModule(cam, E1); 
                }while(frameComplexity_GPU > MAC_GPU);

                if(frameComplexity_GPU < MAC_GPU)
                {
                    seachingInterval *= 0.5f;
                    E1 += seachingInterval;
                    LightweightRenderingWorkloadEstimationModule(cam, E1); 
                    do
                    {
                        if(frameComplexity_GPU < MAC_GPU)
                        {
                            seachingInterval *= 0.5f;
                            E1 += seachingInterval;
                            LightweightRenderingWorkloadEstimationModule(cam, E1); 
                        }
                        else if(frameComplexity_GPU > MAC_GPU)
                        {
                            seachingInterval *= 0.5f; 
                            E1 -= seachingInterval;
                            LightweightRenderingWorkloadEstimationModule(cam, E1); 
                        }
                        else if(frameComplexity_GPU == MAC_GPU)
                        {
                            return; /*successfully get the best eccentricity*/
                        }

                        if(seachingInterval == (MaxEcc - MinEcc) * 0.5f * 0.125f)
                        {
                            if(frameComplexity_GPU < MAC_GPU)
                            {
                                E1 = (E1 + (E1 + seachingInterval)) * 0.5f;
                                return;
                            } 
                            else if(frameComplexity_GPU > MAC_GPU)
                            {
                                E1 = (E1 + (E1 - seachingInterval)) * 0.5f;
                                return;
                            }
                            else if(frameComplexity_GPU == MAC_GPU)
                            {
                                return; /*successfully get the best eccentricity*/
                            }
                        }
                    }while(true);
                }
                else if(frameComplexity_GPU == MAC_GPU) 
                {
                    return; /*successfully get the best eccentricity*/
                }
            }
        } 
    }

    void OfflineBakingMAC()
    {
        lastFrameLatency = Time.deltaTime; 
        // var stopwatch = new Stopwatch();
        // stopwatch.Start();

        var filteredIndex = CheckVisibility(cam, Positions); // get the index of visible gameObjects
        CalculateFrameComplexity(cam, filteredIndex); // compute frame complexity under current eccentricity
        extraLatency = 0; 
        
        // stopwatch.Stop();
        // extraLatency = stopwatch.ElapsedMilliseconds;
    }

    void LightweightRenderingWorkloadEstimationModule(Camera cam, float E1)
    {
        cam.ResetProjectionMatrix();
        _FRutils.RefreshPM(cam, foveaCoordinate, E1);
        cam.cullingMatrix = cam.projectionMatrix * cam.worldToCameraMatrix; 

        var filteredIndex = CheckVisibility(cam, Positions); // get the index of visible gameObjects
        CalculateFrameComplexity(cam, filteredIndex); // compute frame complexity under current eccentricity
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

        // parallel searching
        // Vector4 worldPos, viewPos, projPos; 
        // Matrix4x4 worldToCameraMatrix = cam.worldToCameraMatrix;
        // Matrix4x4 projectionMatrix = cam.projectionMatrix;
        // Vector3 ndcPos, viewportPos;
        // Parallel.ForEach(Positions, pos =>
        // {
        //     worldPos = new Vector4(pos.x, pos.y, pos.z, 1.0f);
        //     viewPos = worldToCameraMatrix * worldPos;
        //     projPos = projectionMatrix * viewPos;
        //     ndcPos = new Vector3(projPos.x / projPos.w, projPos.y / projPos.w, projPos.z / projPos.w);
        //     viewportPos = new Vector3(ndcPos.x * 0.5f + 0.5f, ndcPos.y * 0.5f + 0.5f, -viewPos.z);
        //     if(viewportPos.z > 0 && viewportPos.x >= 0 && viewportPos.x <= 1 && viewportPos.y >= 0 && viewportPos.y <= 1) 
        //     {
        //         filteredIndex.Add(index);
        //     }
        //     index++;
        // });
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
