using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LIWCutils
{
    // Parameters for calculating vertices amount
    Renderer[] gameObjsRenderers;
    Renderer[] filteredRenderers;
    Mesh[] meshes;
    public int totalVerticesCount;

    // Parameters for LIWC component
    float GPU_m; // vertices / second
    float throughPut; // 1Gbps network
    float remoteDimension; // dimension of remotelayer
    int lastFrameIndex;
    List<TableContent> MappingTable;
    public class TableContent
    {
        public TableContent(){}
        public uint motionIndex { get; set; } // 6bits transform, 2bits eye coordinate
        public float eccDelta { get; set; } // use ratio rather than degree
        public float qualityScore { get; set; }
    }

    // parameters used for calculating DataSize(M+O)
    float _W = 17f; // mobile device actual width in (cm)
    float _V = 5f; // The distance between user and screen
    float _D = 2400f; // mobile device horizontal resolution
    float _alpha = 0.45f; // screen ratio: height/width (<=1)

    // parameters used for calculating Quality score
    float lr = 0.5f; // The learning rate in the Bellman Equation
    float reward; // reward according to last frame latency
    System.Random ran = new System.Random();
    

    // Constructor
    public LIWCutils(GameObject scene)
    {
        // Declare mapping table
        MappingTable = new List<TableContent>(); 
        createMappingTable();

        // Pre-fetch all the meshes in given Scene
        prefetchMesh(scene);
    }

    /**
    * @description: Initialize the motion to eccentricity mapping table
    * @param {void}
    * @return {void} 
    */
    void createMappingTable()
    {
        Debug.Log("Start Creating MappingTable...");
        uint motionIndexValue = 0b_00_0000_0000;
        int eccDegreeDelta = -5;
        for(int i = 0; i < 11264; i++) // (2^10) * 11 = 11264
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
                motionIndexValue += 0b_00_0000_0001;
            }
        } 
        Debug.Log("Finish Creating MappingTable...");
    }

    /**
    * @description: Pre-fetch all the meshes in current Scene, which can save time when counting vertices during run-time
    * @param {GameObject scene}: The whole scene that contains the objects
    * @return {void} 
    */
    public void prefetchMesh(GameObject scene)
    {
        gameObjsRenderers = scene.GetComponentsInChildren<Renderer>(); 
        int index = 0;
        List<int> successIndex = new List<int>();
        List<int> failIndex = new List<int>();
        Mesh tmpMesh;
        // Some gameObjsRenderers do not have Mesh component but will still be included, like smoke or other particle effects...
        // so this for loop is to find the index of those objects
        foreach (Renderer r in gameObjsRenderers)
        {
            try
            {
                tmpMesh = r.gameObject.GetComponent<MeshFilter>().mesh; 
                successIndex.Add(index);
            }
            catch(Exception e)
            {
                failIndex.Add(index);
                var str = e.Message; // to elminate the annoying warning
            }
            index++;
        }
        // filter gameObjsRenderers according to the success index
        // after this loop, filteredRenderers now all contain Mesh component
        filteredRenderers = new Renderer[successIndex.Count];
        index = 0;
        foreach (var item in successIndex)
        {
            filteredRenderers[index] = gameObjsRenderers[item];
            index++;
        }
        // get Mesh component of all filteredRenderers in advance
        // because the profiler shows that low-level getMesh() is really time-consuming
        meshes = new Mesh[filteredRenderers.Length];
        index = 0;
        foreach (Renderer r in filteredRenderers)
        {
            meshes[index] = r.gameObject.GetComponent<MeshFilter>().mesh;
            index++;
        }
    }

    /**
    * @description: convert the world position of the chosen gameobjects to the view position
    * @param {Camera cam, Transform[] gameObjsTranforms} 
    * @return {Vector3[] viewPoss} 
    */
    Vector3[] worldPos2ViewPos(Camera cam, Renderer[] gameObjsRenderers)
    {
        Vector3[] viewPoss = new Vector3[gameObjsRenderers.Length];
        int index = 0;
        foreach (Renderer r in gameObjsRenderers)
        {
            Vector3 viewPos = cam.WorldToViewportPoint(r.gameObject.transform.position);
            viewPoss[index] = viewPos;
            index++;
        }
        return viewPoss;
    }

    /**
    * @description: calculate total visible vertices amount with respect to give E1, foveaCoordinate
    * @param {Vector3[] gameObjsViewPos, float E1, Vector2 foveaCoordinate} 
    * @return {int totalVerticesCount} 
    */
    public void totalVertsCounter(Camera cam, float E1, Vector2 foveaCoordinate)
    {
        Vector3[] gameObjsViewPos = worldPos2ViewPos(cam, filteredRenderers);

        // refresh the border coordinate of the rectangle formed by new E1 and foveaCoordinate
        float left = (foveaCoordinate.x - E1 < 0) ? 0 : (foveaCoordinate.x - E1);
        float right = (foveaCoordinate.x + E1 > 1) ? 1 : (foveaCoordinate.x + E1);
        float bottom = (foveaCoordinate.y - E1 < 0) ? 0 : (foveaCoordinate.y - E1);
        float top = (foveaCoordinate.y + E1 > 1) ? 1 : (foveaCoordinate.y + E1);

        totalVerticesCount = 0;
        int index = 0;
        foreach (var viewPos in gameObjsViewPos)
        {
            // if both x and y are within the range of [0,1] and the rectangle formed by foveaCoordinate and eccentricity, current gameObj is visible to camera
            if(viewPos.x >= left && viewPos.x <= right && viewPos.y >= bottom && viewPos.y <= top) 
            {
                totalVerticesCount += meshes[index].vertexCount; 
            }
            index++;
        }
    }

    /**
    * @description: generate mapping index according to the transform in the last frame and the current transform
    * @param {oldTrans: the transform in the last frame
                newTrans: the current transform
                oldCoord: eye coordinate in the last frame
                newCoord: the current eye coordinate}
    * @return {mappingIndex: generated mapping index} 
    */
    public uint generateMappingIndex(Vector3 oldPos, Vector3 newPos, Quaternion oldRot, Quaternion newRot, Vector2 oldCoord, Vector2 newCoord, 
                                    float newFrameComplexity_cpu, float oldFrameComplexity_cpu,
                                    float newFrameComplexity_gpu, float oldFrameComplexity_gpu)
    {
        Vector3 positionDelta = newPos - oldPos;
        Vector3 rotationDelta = newRot.eulerAngles - oldRot.eulerAngles;
        Vector2 coordDelta = newCoord - oldCoord;
        float frameComplexityDelta_cpu = newFrameComplexity_cpu - oldFrameComplexity_cpu;
        float frameComplexityDelta_gpu = newFrameComplexity_gpu - oldFrameComplexity_gpu;
        uint mappindIndex = 0b_00_0000_0000;

        // assign values for all bits
        if(frameComplexityDelta_cpu >= 0.0f) mappindIndex |= 0b_10_0000_0000; // change corresponding bit to 1
        else mappindIndex |= 0b_00_0000_0000; // remain unchanged

        if(frameComplexityDelta_gpu >= 0.0f) mappindIndex |= 0b_01_0000_0000; 
        else mappindIndex |= 0b_00_0000_0000; 

        if(positionDelta.x >= 0.0f) mappindIndex |= 0b_00_1000_0000; 
        else mappindIndex |= 0b_00_0000_0000; 

        if(positionDelta.y >= 0.0f) mappindIndex |= 0b_00_0100_0000;
        else mappindIndex |= 0b_00_0000_0000;

        if(positionDelta.z >= 0.0f) mappindIndex |= 0b_00_0010_0000;
        else mappindIndex |= 0b_00_0000_0000;

        if(rotationDelta.x >= 0.0f) mappindIndex |= 0b_00_0001_0000;
        else mappindIndex |= 0b_00_0000_0000;

        if(rotationDelta.y >= 0.0f) mappindIndex |= 0b_00_0000_1000;
        else mappindIndex |= 0b_00_0000_0000;

        if(rotationDelta.z >= 0.0f) mappindIndex |= 0b_00_0000_0100;
        else mappindIndex |= 0b_00_0000_0000;
        
        if(coordDelta.x >= 0.0f) mappindIndex |= 0b_00_0000_0010;
        else mappindIndex |= 0b_00_0000_0000;

        if(coordDelta.y >= 0.0f) mappindIndex |= 0b_00_0000_0001;
        else mappindIndex |= 0b_00_0000_0000;

        return mappindIndex;
    }

    /**
    * @description: generate Delta Eccentricity index according to frameCount, Epsilon greed strategy is adopted
    * @param {frameCount: frame count}
    * @return {Delta Eccentricity index} 
    */
    private int generateDeltaEccIndex(int frameCount, float[] qualityScoreArray)
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

    /**
    * @description: select Best Eccentricity for current frame
    * @param {frameCount: frameCount for this frame
                mappindIndex: the generated mapping index for this frame
                lastFrameE1ratio: selected best eccentricity in the last frame
                lastFrameLatency: latency of last frame}
    * @return {E1: predicted best eccentricity for this frame} 
    */
    public float selectBestEccentricity(int frameCount, uint mappingIndex, float lastFrameE1ratio, float lastFrameLatency)
    {
        // Compute the reward of frame N-1 according to the latency of frame N-1
        if(lastFrameLatency <= 0.02f) reward = 1.0f;
        else reward = -1.0f;

        // Update the Q-table value according to Delta Ecc index of frame N-1 and the reward of frame N-1
        // (Delta Ecc index hasn't been updated now, which is still the value in frame N-1)
        if(frameCount > 1) // The first frame doesn't have lastFrameIndex info
        {
            float oldScore = MappingTable[lastFrameIndex].qualityScore;
            MappingTable[lastFrameIndex].qualityScore = (1 - lr) * oldScore + lr * reward;
        }

        // parameters used for calculating DataSize(M+O)
        // float _omega = 180 * Mathf.Atan(2 * _W / (_V * _D)) / Mathf.PI; // The minimum MAR supported by screen
        // float m = 0.028f; // MAR model slope
        // float _omega0 = 1f/48f; 
        // float _E1Ratio;
        // float _E1Degree;
        // float S2;
        // float D2; // dimension of remote layer

        // float T_remote;
        // float T_local;
        // float diff; // difference between T_remote and T_local
        // float minimumDiff = 1000; 
        // int corresIndex = 0; // The index that gives the minimum difference

        // Calculate and get the minimum T_local and T_remote difference one by one
        // for(int index = startIndex; index < startIndex + 11; index++)
        // {  
        //     // calculate the dimension of remote layer
        //     _E1Ratio = MappingTable[index].eccDelta + lastFrameE1ratio;
        //     _E1Degree = 180 * Mathf.Atan(_E1Ratio * _W / _V) / Mathf.PI;
        //     S2 = (m * _E1Degree + _omega0) / _omega;
        //     D2 = _D / S2;

        //     // calculate T_local and T_remote
        //     T_local = totalVerticesCount * 2 * _E1Ratio / GPU_m; // %fovea is equal to 2 * _E1Ratio 
        //     T_remote = D2 * D2 * _alpha * 4f / 1024f / 1024f / throughPut;

        //     // refresh the minimum latency difference and record the corresponding index
        //     diff = Mathf.Abs(T_local - T_remote);
        //     if(diff < minimumDiff)
        //     {
        //         minimumDiff = diff;
        //         corresIndex = index;
        //         remoteDimension = D2;
        //     }
        // }
        
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
        // regulate it before outputing
        float Ecc = lastFrameE1ratio + MappingTable[startIndex + DeltaEccIndex].eccDelta;
        if(Ecc > 0.5f) Ecc = 0.5f;
        else if(Ecc < 0.05f) Ecc = 0.05f;

        return Ecc;
    }

    public void refreshGPUm(float latency)
    {
        GPU_m = totalVerticesCount / latency;
    }

    public void refreshThroughPut(float latency)
    {
        throughPut = remoteDimension * remoteDimension * _alpha * 4f / latency;
    }



    //////////////////////////////////////////////////////////// self-designed best eccentricity searching algorithm //////////////////////////////////////////////////////////
    int verticeLimit = 1000000;
    float error = 100000;
    float searchStep = 0.5f;
    int searchTimes = 0;
    List<int> verticesAmount;
    List<float> clientLatency;
    double rSquared, intercept, slope; // parameters for the estimated curve

    /**
    * @description: Search for the best eccentricity according to vertices amount
    * @param {Vector3[] gameObjsViewPos, float initialE1, Vector2 foveaCoordinate} 
    * @return {float E1} 
    */
    public float SearchBestEccentricity(Camera cam, float initialE1, Vector2 foveaCoordinate)
    {
        float E1 = initialE1;
        int[] totalVerticesCountBuffer = new int[3];
        searchTimes = 0;

        while(true)
        {
            totalVertsCounter(cam, E1, foveaCoordinate);
            searchTimes++;

            totalVerticesCountBuffer[0] = totalVerticesCountBuffer[1]; // this is to prevent search algorithm stuck in an specific area
            totalVerticesCountBuffer[1] = totalVerticesCountBuffer[2];
            totalVerticesCountBuffer[2] = totalVerticesCount;
            if(totalVerticesCountBuffer[0] == totalVerticesCountBuffer[2] && totalVerticesCountBuffer[1] != totalVerticesCountBuffer[2] && totalVerticesCountBuffer[2] != 0) searchStep /= 2;
            
            if(totalVerticesCount < verticeLimit - error) E1 += searchStep; // should increase E1 to reduce network bandwidth requirments
            else if(totalVerticesCount > verticeLimit + error) E1 -= searchStep; // should decrease E1 to maintain Client FPS
            else return E1; // return best E1
        }
    }

    /**
    * @description: Fits a line to a collection of (latency, vertices) points to evaluate Client performance
    * @param {verticesAmount: The list of vertices amount in each frame
                clientLatency: The list of time consumption between adjcent frames
                rSquared: The r^2 value of the estimated curve
                yIntercept: The y-intercept value of the estimated curve (i.e. y = ax + b, yIntercept is b)
                slope: The slop of the estimated curve (i.e. y = ax + b, slope is a)} 
    * @return {void} 
    */
    public void LinearRegression(
        int[] xVals,
        float[] yVals,
        out double rSquared,
        out double yIntercept,
        out double slope)
    {
        if (xVals.Length != yVals.Length)
        {
            throw new Exception("Input values should be with the same length.");    
        }

        double sumOfX = 0;
        double sumOfY = 0;
        double sumOfXSq = 0;
        double sumOfYSq = 0;
        double sumCodeviates = 0;

        for (var i = 0; i < xVals.Length; i++)
        {
            var x = xVals[i];
            var y = yVals[i];
            sumCodeviates += x * y;
            sumOfX += x;
            sumOfY += y;
            sumOfXSq += x * x;
            sumOfYSq += y * y;
        }
        
        var count = xVals.Length;
        var ssX = sumOfXSq - ((sumOfX * sumOfX) / count);
        var ssY = sumOfYSq - ((sumOfY * sumOfY) / count);

        var rNumerator = (count * sumCodeviates) - (sumOfX * sumOfY);
        var rDenom = (count * sumOfXSq - (sumOfX * sumOfX)) * (count * sumOfYSq - (sumOfY * sumOfY));
        var sCo = sumCodeviates - ((sumOfX * sumOfY) / count);

        var meanX = sumOfX / count;
        var meanY = sumOfY / count;
        var dblR = rNumerator / Math.Sqrt(rDenom);

        rSquared = dblR * dblR;
        yIntercept = meanY - ((sCo / ssX) * meanX);
        slope = sCo / ssX;
        // Debug.Log(string.Format("yIntercept is: {0}, slope is: {1}", yIntercept, slope));
    }

}
