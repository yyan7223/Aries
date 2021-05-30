using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TestFPS : MonoBehaviour
{
    private float initialE1Ratio = 0.03f; 
    private float currentE1Ratio; 
    private float gap = 0.01f;
    private int frameCount = 0;
    private float FPS = 0;
    private float startTime;
    private float endTime;
    private Camera cam;
    private RenderTexture RT;
    private Text screenText;
    double m_gpuFrameTime;
    double m_cpuFrameTime;
    private float targetFPS = 240f;
    private Vector2 foveaCoordinate; 
    Vector4 defaultClipPlaneRLTB;
    private int accumulateTris;
    private int accumulateVerts;
    private bool haventSaveResults = true;
    List<TrisVerts> TrisVertsResults;

    // Start is called before the first frame update
    void Start()
    {
        screenText = transform.Find("Canvas").gameObject.GetComponent<Text>();
        
        foveaCoordinate = new Vector2(0.5f, 0.5f);
        cam = transform.Find("Camera").gameObject.GetComponent<Camera>();
        defaultClipPlaneRLTB = ComputeDefaultclipPlaneRLTB(cam);
        currentE1Ratio = initialE1Ratio;
        RefreshPM(cam, foveaCoordinate, currentE1Ratio);

        RT = new RenderTexture(
                    (int)(UnityEngine.Screen.width * initialE1Ratio * 2),
                    (int)(UnityEngine.Screen.height * initialE1Ratio * 2),
                    16, RenderTextureFormat.ARGB32
                );
        startTime = Time.time;
        cam.targetTexture = RT;
    }

    // Update is called once per frame
    void Update()
    {
        if(TrisVertsResults == null) TrisVertsResults = new List<TrisVerts>();

        // accumulateTris += UnityEditor.UnityStats.triangles;
        // accumulateVerts += UnityEditor.UnityStats.vertices;

        if(frameCount == targetFPS)
        {
            endTime = Time.time;
            FPS = targetFPS * 1f / (endTime - startTime);
            // Debug.Log(string.Format("Resolution: {0}x{1}; FPS:{2}", 
            //                         UnityEngine.Screen.width * currentE1Ratio * 2,
            //                         UnityEngine.Screen.height * currentE1Ratio * 2,
            //                         FPS));
            screenText.text = string.Format("Resolution: {0}x{1}\nAverageFrameTime: {2:F4}ms",
                                            UnityEngine.Screen.width * currentE1Ratio * 2,
                                            UnityEngine.Screen.height * currentE1Ratio * 2,
                                            1000*(endTime - startTime)/targetFPS);
            
            // Record Tris and Verts
            TrisVertsResults.Add(new TrisVerts{averageTrisNum = accumulateTris / frameCount / 1000f,
                                        averageVertsNum = accumulateVerts / frameCount / 1000f});
            currentE1Ratio += gap;

            if(currentE1Ratio > 0.48f && haventSaveResults)
            {
                Debug.Log("Start saving ClientTrisVertsResults");
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("averageTrisNum(k),  averageVertsNum(k)");
                foreach (var item in TrisVertsResults)
                {
                    sb.AppendLine(item.ToString());
                }

                Console.WriteLine(sb.ToString());
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(
                    Application.dataPath, "TwoOrThreeLayerSQVRResults", "ClientTrisVerts.txt"), 
                    sb.ToString());
                Console.ReadLine();
                Debug.Log("ClientTrisVertsResults sucessfully saved");
                haventSaveResults = false; // Only save results for once
            }

            RefreshRTResolution(currentE1Ratio);
            RefreshPM(cam, foveaCoordinate, currentE1Ratio);

            frameCount = 0;
            accumulateTris = 0;
            accumulateVerts = 0;
            startTime = endTime;
        }

        frameCount++;
    }

    void RefreshRTResolution(float currentE1Ratio)
    {
        RT = new RenderTexture(
                    (int)(UnityEngine.Screen.width * currentE1Ratio * 2),
                    (int)(UnityEngine.Screen.height * currentE1Ratio * 2),
                    16, RenderTextureFormat.ARGB32
                );
        cam.targetTexture = RT;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(RT, destination);
    }

    void RefreshPM(Camera cam, Vector2 fovea_coordinate, float eccentricity)
    {
        Matrix4x4 ProjMat;

        // Convert the foveat coordinates in the viewport to the coordinates in clip plane. 
        // (0.5f, 0.5f) is the normalized coordinate of the center point in the viewport
        // The center point in the clip plane is (0,0)
        Vector2 foveaInClipPlane = new Vector2(
            0.0f + (fovea_coordinate.x - 0.5f) * (defaultClipPlaneRLTB.x - defaultClipPlaneRLTB.y),
            0.0f + (fovea_coordinate.y - 0.5f) * (defaultClipPlaneRLTB.z - defaultClipPlaneRLTB.w)
        );

        // Compute the value of right, left, top, bottom of the cam_fovea on the near clipping plane 
        Vector4 clipPlaneRLTB = ComputeClipPlaneRLTB(fovea_coordinate, eccentricity, foveaInClipPlane, defaultClipPlaneRLTB);

        // Compute the projection matrix of the cam_fovea according to clipPlaneRLTB_fovea
        ProjMat = cam.projectionMatrix;
        ProjMat.m00 = 2 * cam.nearClipPlane / (clipPlaneRLTB.x - clipPlaneRLTB.y);
        ProjMat.m11 = 2 * cam.nearClipPlane / (clipPlaneRLTB.z - clipPlaneRLTB.w);
        ProjMat.m02 = (clipPlaneRLTB.x + clipPlaneRLTB.y) / (clipPlaneRLTB.x - clipPlaneRLTB.y);
        ProjMat.m12 = (clipPlaneRLTB.z + clipPlaneRLTB.w) / (clipPlaneRLTB.z - clipPlaneRLTB.w);

        cam.projectionMatrix = ProjMat;
    }

    Vector4 ComputeDefaultclipPlaneRLTB(Camera cam)
    {
        Matrix4x4 defaultProjectionMatrix = cam.projectionMatrix;
        defaultClipPlaneRLTB = new Vector4(
            cam.nearClipPlane / defaultProjectionMatrix[0, 0], // right
            -cam.nearClipPlane / defaultProjectionMatrix[0, 0], // left
            cam.nearClipPlane / defaultProjectionMatrix[1, 1], // top
            -cam.nearClipPlane / defaultProjectionMatrix[1, 1] // bottom
        );
        return defaultClipPlaneRLTB;
    }

    Vector4 ComputeClipPlaneRLTB(Vector2 fovea_coordinate, float eccentricity, Vector2 foveaInClipPlane, Vector4 defaultClipPlaneRLTB)
    {
        // if larger than border, then set the value equals to border
        // if smaller than border, compute the value normally
        float right = (fovea_coordinate.x + eccentricity >= 1.0f) ? defaultClipPlaneRLTB.x : (foveaInClipPlane.x + eccentricity * (defaultClipPlaneRLTB.x - defaultClipPlaneRLTB.y));
        float left = (fovea_coordinate.x - eccentricity <= 0.0f) ? defaultClipPlaneRLTB.y : (foveaInClipPlane.x - eccentricity * (defaultClipPlaneRLTB.x - defaultClipPlaneRLTB.y));
        float top = (fovea_coordinate.y + eccentricity >= 1.0f) ? defaultClipPlaneRLTB.z : (foveaInClipPlane.y + eccentricity * (defaultClipPlaneRLTB.z - defaultClipPlaneRLTB.w));
        float bottom = (fovea_coordinate.y - eccentricity <= 0.0f) ? defaultClipPlaneRLTB.w : (foveaInClipPlane.y - eccentricity * (defaultClipPlaneRLTB.z - defaultClipPlaneRLTB.w));
        return new Vector4(right, left, top, bottom);
    }

    public class TrisVerts
    {
        public TrisVerts()
        {
        }
        public float averageTrisNum { get; set; }
        public float averageVertsNum { get; set; }
        public override string ToString()
        {
            return this.averageTrisNum.ToString("F3") + "  " 
                    + this.averageVertsNum.ToString("F3");
        }
    }


    // public Text screenText;

    // FrameTiming[] frameTimings = new FrameTiming[3];

    // public float maxResolutionWidthScale = 1.0f;
    // public float maxResolutionHeightScale = 1.0f;
    // public float minResolutionWidthScale = 0.5f;
    // public float minResolutionHeightScale = 0.5f;
    // public float scaleWidthIncrement = 0.1f;
    // public float scaleHeightIncrement = 0.1f;

    // float m_widthScale = 1.0f;
    // float m_heightScale = 1.0f;

    // // Variables for dynamic resolution algorithm that persist across frames
    // uint m_frameCount = 0;

    // const uint kNumFrameTimings = 2;

    // double m_gpuFrameTime;
    // double m_cpuFrameTime;

    // // Use this for initialization
    // void Start()
    // {
    //     screenText = transform.Find("Canvas").gameObject.GetComponent<Text>();
    //     int rezWidth = (int)Mathf.Ceil(ScalableBufferManager.widthScaleFactor * Screen.currentResolution.width);
    //     int rezHeight = (int)Mathf.Ceil(ScalableBufferManager.heightScaleFactor * Screen.currentResolution.height);
    //     screenText.text = string.Format("Scale: {0:F3}x{1:F3}\nResolution: {2}x{3}\nScreenRefreshRate: {4}Hz",
    //         m_widthScale,
    //         m_heightScale,
    //         rezWidth,
    //         rezHeight,
    //         Screen.currentResolution.refreshRate);
    // }

    // // Update is called once per frame
    // void Update()
    // {
    //     if(frameCount == 0){
    //         startTime = Time.time;
    //     }
    //     else if(frameCount == 90)
    //     {
    //         endTime = Time.time;
    //         FPS = 90f * 1f / (endTime - startTime);
    //         frameCount = 0;
    //         startTime = endTime;
    //     }
    //     frameCount++;

    //     float oldWidthScale = m_widthScale;
    //     float oldHeightScale = m_heightScale;

    //     // One finger lowers the resolution
    //     if (Input.GetButtonDown("Fire1"))
    //     {
    //         m_heightScale = Mathf.Max(minResolutionHeightScale, m_heightScale - scaleHeightIncrement);
    //         m_widthScale = Mathf.Max(minResolutionWidthScale, m_widthScale - scaleWidthIncrement);
    //     }

    //     // Two fingers raises the resolution
    //     if (Input.GetButtonDown("Fire2"))
    //     {
    //         m_heightScale = Mathf.Min(maxResolutionHeightScale, m_heightScale + scaleHeightIncrement);
    //         m_widthScale = Mathf.Min(maxResolutionWidthScale, m_widthScale + scaleWidthIncrement);
    //     }

    //     if (m_widthScale != oldWidthScale || m_heightScale != oldHeightScale)
    //     {
    //         ScalableBufferManager.ResizeBuffers(m_widthScale, m_heightScale);
    //     }
    //     DetermineResolution();
    //     int rezWidth = (int)Mathf.Ceil(ScalableBufferManager.widthScaleFactor * Screen.currentResolution.width);
    //     int rezHeight = (int)Mathf.Ceil(ScalableBufferManager.heightScaleFactor * Screen.currentResolution.height);
    //     screenText.text = string.Format("Scale: {0:F3}x{1:F3}\nResolution: {2}x{3}\nScaleFactor: {4:F3}x{5:F3}\nGPU: {6:F3} CPU: {7:F3}\nFPS: {8}\nScreenRefreshRate: {9}Hz",
    //         m_widthScale,
    //         m_heightScale,
    //         rezWidth,
    //         rezHeight,
    //         ScalableBufferManager.widthScaleFactor,
    //         ScalableBufferManager.heightScaleFactor,
    //         m_gpuFrameTime,
    //         m_cpuFrameTime,
    //         FPS,
    //         Screen.currentResolution.refreshRate);
    // }

    // // Estimate the next frame time and update the resolution scale if necessary.
    // private void DetermineResolution()
    // {
    //     ++m_frameCount;
    //     if (m_frameCount <= kNumFrameTimings)
    //     {
    //         return;
    //     }
    //     FrameTimingManager.CaptureFrameTimings();
    //     FrameTimingManager.GetLatestTimings(kNumFrameTimings, frameTimings);
    //     if (frameTimings.Length < kNumFrameTimings)
    //     {
    //         Debug.LogFormat("Skipping frame {0}, didn't get enough frame timings.",
    //             m_frameCount);

    //         return;
    //     }

    //     m_gpuFrameTime = (double)frameTimings[0].gpuFrameTime;
    //     m_cpuFrameTime = (double)frameTimings[0].cpuFrameTime;
    // }
}
