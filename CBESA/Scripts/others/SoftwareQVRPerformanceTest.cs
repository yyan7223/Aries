using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

public class SoftwareQVRPerformanceTest : MonoBehaviour
{
    private FRutils _FRutils;
    public Camera cam;

    List<PerformanceData> PerformanceDataset;
    Vector2 foveaCoordinate = new Vector2(0.5f, 0.5f);
    float E1 = 0.5f;

    // best eccentricity selecting related parameters
    System.Random ran = new System.Random();
    float lr = 0.5f; // The learning rate in the Bellman Equation
    float reward; // reward according to last frame latencyint lastFrameIndex;
    Vector3 oldPos, newPos;
    Quaternion oldRot, newRot;
    Vector2 oldCoord, newCoord;
    uint mappingIndex;
    int lastFrameIndex;
    List<TableContent> MappingTable;
    public class TableContent
    {
        public TableContent(){}
        public uint motionIndex { get; set; } // 6bits transform, 2bits eye coordinate
        public float eccDelta { get; set; } // use ratio rather than degree
        public float qualityScore { get; set; }
    }


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
    float lastFrameLatency;
    float lastFrameEccentricity;
    int frameCount = 0;

    public class PerformanceData
    {
        public PerformanceData()
        {
        }
        public float latencyMilliSecond { get; set; }
        public float eccentricity { get; set; }
        public override string ToString()
        {
            return this.latencyMilliSecond.ToString("F4") + "                         " 
            + this.eccentricity.ToString("F4");
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        _FRutils = new FRutils(cam); 
        Application.targetFrameRate = 90;
        PerformanceDataset = new List<PerformanceData>();
        MappingTable = new List<TableContent>(); 
        createMappingTable();

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
            xCount = 0;
            zCount++;
        }
        if(zCount == zMoveCount)
        {
            saveTextFile();
            Application.Quit();
        }
        frameCount++;

        /* run-time Best eccentricity selecting algorithm */
        RunTimeBestEccSelect();

        if(frameCount > 0 && frameCount < xMoveCount * zMoveCount * yRotateCount + 1)
        {
            PerformanceDataset.Add(new PerformanceData{latencyMilliSecond = lastFrameLatency * 1000,
                                                        eccentricity = lastFrameEccentricity});  // store all the data of last frame                      
        }

        // let current Transform and eyes coordinates to be old values
        oldPos = transform.position;
        oldRot = transform.rotation;
        oldCoord = foveaCoordinate;
        lastFrameEccentricity = E1;
    }

    private void OnDisable()
    {
        // Remove WriteLogMessage as a delegate of the  RenderPipelineManager.beginCameraRendering event
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    void RunTimeBestEccSelect()
    {
        lastFrameLatency = Time.deltaTime; // the rendering time of last frame
        if(frameCount > 0)
        {
            // Generate mapping index according to the old value and new value
            newPos = transform.position;
            newRot = transform.rotation;
            newCoord = foveaCoordinate;
            mappingIndex = generateMappingIndex(oldPos, newPos, oldRot, newRot, oldCoord, newCoord);
            
            // get best E1
            E1 = selectBestEccentricity(frameCount, mappingIndex, lastFrameEccentricity, lastFrameLatency);
            
            // regulate E1
            if(E1 > 0.5f) E1 = 0.5f;
            if(E1 < 0.05f) E1 = 0.05f;
        }
        
        // refresh camera PM according to best E1
        cam.ResetProjectionMatrix();
        _FRutils.RefreshPM(cam, foveaCoordinate, E1);
        cam.cullingMatrix = cam.projectionMatrix * cam.worldToCameraMatrix; 
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
                Application.dataPath, "softwareQVRPerformance.txt"), 
                sb.ToString());
        }
        else if(Application.platform == RuntimePlatform.Android)
        {
            // Go to your Player settings, for Android, Change Write access from "Internal Only" to External (SDCard). 
            // You can then use Application.persistentDataPath to get the location of your external storage path.
            // Application.persistentDataPath on android points to /storage/emulated/0/Android/data/<packagename>/files on most devices
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(
                Application.persistentDataPath, "softwareQVRGPUPerformance.txt"),
                sb.ToString());
        }
        Console.ReadLine();
    }

    void createMappingTable()
    {
        UnityEngine.Debug.Log("Start Creating MappingTable...");
        uint motionIndexValue = 0b_0000_0000;
        int eccDegreeDelta = -5;
        for(int i = 0; i < 2816; i++) // (2^8) * 11 = 2816
        {
            MappingTable.Add(new TableContent{
                motionIndex = motionIndexValue,
                eccDelta = eccDegreeDelta * 0.01f,
                qualityScore = 0.0f
            });
            eccDegreeDelta++;

            if(eccDegreeDelta > 5)
            {
                eccDegreeDelta = -5; // reset value
                motionIndexValue += 0b_0000_0001;
            }
        } 
        UnityEngine.Debug.Log("Finish Creating MappingTable...");
    }

    uint generateMappingIndex(Vector3 oldPos, Vector3 newPos, Quaternion oldRot, Quaternion newRot, Vector2 oldCoord, Vector2 newCoord)
    {
        Vector3 positionDelta = newPos - oldPos;
        Vector3 rotationDelta = newRot.eulerAngles - oldRot.eulerAngles;
        Vector2 coordDelta = newCoord - oldCoord;
        uint mappindIndex = 0b_0000_0000;

        // assign values for all bits
        if(positionDelta.x >= 0.0f) mappindIndex |= 0b_1000_0000; // change corresponding bit to 1
        else mappindIndex |= 0b_0000_0000; // remain unchanged

        if(positionDelta.y >= 0.0f) mappindIndex |= 0b_0100_0000;
        else mappindIndex |= 0b_0000_0000;

        if(positionDelta.z >= 0.0f) mappindIndex |= 0b_0010_0000;
        else mappindIndex |= 0b_0000_0000;

        if(rotationDelta.x >= 0.0f) mappindIndex |= 0b_0001_0000;
        else mappindIndex |= 0b_0000_0000;

        if(rotationDelta.y >= 0.0f) mappindIndex |= 0b_0000_1000;
        else mappindIndex |= 0b_0000_0000;

        if(rotationDelta.z >= 0.0f) mappindIndex |= 0b_0000_0100;
        else mappindIndex |= 0b_0000_0000;
        
        if(coordDelta.x >= 0.0f) mappindIndex |= 0b_0000_0010;
        else mappindIndex |= 0b_0000_0000;

        if(coordDelta.y >= 0.0f) mappindIndex |= 0b_0000_0001;
        else mappindIndex |= 0b_0000_0000;

        return mappindIndex;
    }

    int generateDeltaEccIndex(int frameCount, float[] qualityScoreArray)
    {
        // get the QmaxIndex
        int QmaxIndex = 0;
        if(frameCount == 1) 
        {
            // all Quality score in the array is zero in the first frame, so just set the index to 0
            QmaxIndex = 0;
        }
        else
        {
            // find the index of Max Quality score value
            float Qmaxvalue = 0;
            for(int index = 0; index < qualityScoreArray.Length; index++)
            {
                if (qualityScoreArray[index] > Qmaxvalue)
                {
                    Qmaxvalue = qualityScoreArray[index];
                    QmaxIndex = index;
                }
            }
        }

        // generate the random index
        int randomIndex = ran.Next(0,11); // genrate int number within the range [0, 10]

        // generate DeltaEccIndex (Rounded result)
        int DeltaEccIndex = Convert.ToInt32(randomIndex * Math.Exp(-0.01f * frameCount) + QmaxIndex * (1 - Math.Exp(-0.01f * frameCount)));

        // regulate to correct range
        if(DeltaEccIndex > 10) DeltaEccIndex = 10;
        else if(DeltaEccIndex < 0) DeltaEccIndex = 0;

        return DeltaEccIndex;
    }

    float selectBestEccentricity(int frameCount, uint mappingIndex, float lastFrameE1ratio, float lastFrameLatency)
    {
        // Compute the reward of frame N-1 according to the latency of frame N-1
        if(lastFrameLatency <= 0.01666666f) reward = 1.0f;
        else reward = -1.0f;

        // Update the Q-table value according to Delta Ecc index of frame N-1 and the reward of frame N-1
        // (Delta Ecc index hasn't been updated now, which is still the value in frame N-1)
        if(frameCount > 1) // The first frame doesn't have lastFrameIndex info
        {
            float oldScore = MappingTable[lastFrameIndex].qualityScore;
            MappingTable[lastFrameIndex].qualityScore = (1 - lr) * oldScore + lr * reward;
        }
        
        // Choose Delta Ecc of frame N according to the mappingIndex of frame N and the epsilon greedy strategy
        int startIndex = Convert.ToInt32(mappingIndex) * 11;
        float[] qualityScoreArray = new float[11];
        for(int i = 0; i< 11; i++)
        {
            // one state corresponds to 11 action, we need to get the quality score of each action
            qualityScoreArray[i] = MappingTable[startIndex + i].qualityScore;
        }
        int DeltaEccIndex = generateDeltaEccIndex(frameCount, qualityScoreArray);
        // Debug.Log(string.Format("frame: {0}, lastFrameE1ratio is: {1}, lastFrameLatency is: {2}, Index is: {3} {4}", frameCount, lastFrameE1ratio, lastFrameLatency, startIndex, DeltaEccIndex));

        // refresh the Delta Ecc Index from frame N-1 to frame N
        lastFrameIndex = startIndex + DeltaEccIndex;

        // Generate the predicted Eccentricity of frame N according to the Delta Ecc in frame N
        // and the initial Ecc of frame N, which is also the Ecc of frame N-1
        return lastFrameE1ratio + MappingTable[startIndex + DeltaEccIndex].eccDelta;
    }
}
