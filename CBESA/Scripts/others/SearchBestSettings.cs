using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SearchBestSettings : MonoBehaviour
{
    private float _W = 17f; // mobile device actual width in (cm)
    private float _V = 5f; // The distance between user and screen
    private float _D = 2400f; // mobile device horizontal resolution
    private float _alpha = 0.45f; // screen ratio: height/width (<=1)

    // Eccentricity range that satisfied local rending higher than 90 FPS
    private float[] _E1Ratio = new float[46];  // in code 
    private float[] _E1Degree = new float[46]; // Eccentricity range in Foveated 3D Graphics paper
    private int searchNum = 50;

    private float _omega; // The minimum MAR supported by screen
    private float _e; // The angular radius for the whole screen, half of FOV

    private float m = 0.028f; // MAR model slope
    private float _omega0 = 1f/48f; 
    
    // Start is called before the first frame update
    void Start()
    {
        // Calculate some constant values
        _omega = 180 * Mathf.Atan(2 * _W / (_V * _D)) / Mathf.PI;
        _e = 180 * Mathf.Atan(_W / (_V * 2)) / Mathf.PI;

        // Assign values for _E1Ratio _E1Degree arrays
        int count = 0;
        for(float i = 0.03f; i <= 0.48f; i += 0.01f)
        {
            _E1Ratio[count] = i;
            _E1Degree[count] = 180 * Mathf.Atan(i * _W / _V) / Mathf.PI;
            count++;
        }


        Debug.Log("Start saving TwoLayerResults");
        SaveTwoLayerResults(_E1Ratio, _E1Degree);
        Debug.Log("TwoLayerResults sucessfully saved");

        Debug.Log("Start saving ThreeLayerResults");
        SaveThreeLayerResults(_E1Ratio, _E1Degree);
        Debug.Log("ThreeLayerResults sucessfully saved");

    }

    // calculate and Save 2 layers SQ-VR remoteFrameSize
    void SaveTwoLayerResults(float[] _E1Ratio, float[] _E1Degree)
    {
        List<TwoLayers> TwoLayersResults = new List<TwoLayers>();

        for(int i = 0; i < _E1Ratio.Length; i++)
        {
            float S1 = 1f;
            float S2 = (m * _E1Degree[i] + _omega0) / _omega;
            float D1 = 2 * _D * _V * Mathf.Tan(Mathf.PI * _E1Degree[i] / 180) / (S1 * _W);
            float D2 = _D / S2;
            TwoLayersResults.Add(new TwoLayers{E1Ratio = _E1Ratio[i],
                                                E1Degree = _E1Degree[i],
                                                _D1 = D1,
                                                _D2 = D2,
                                                remoteFrameSize = D2 * D2 * _alpha * 4f / 1024f / 1024f});
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("E1(Ratio),  E1(Degree),  D1,  D2,  RemoteFrameSize(MB)");
        foreach (var item in TwoLayersResults)
        {
            sb.AppendLine(item.ToString());
        }

        Console.WriteLine(sb.ToString());
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(
            Application.dataPath, "TwoOrThreeLayerSQVRResults", "TwoLayersResults.txt"), 
            sb.ToString());
        Console.ReadLine();
    }

    void SaveThreeLayerResults(float[] _E1Ratio, float[] _E1Degree)
    {
        List<ThreeLayers> ThreeLayersResults = new List<ThreeLayers>();

        for(int i = 0; i < _E1Ratio.Length; i++)
        {
            // For each E1Ratio, search for the best E2Ratio that minimizes the Server Total Rendered Frame Size 
            float totalE2RatioRange = 0.5f - _E1Ratio[i];
            float searchInterval = totalE2RatioRange / searchNum;
            float MinimumTotalFrameSize = 100f;
            float optimalE2Degree = 100f;
            float optimalE2Ratio = 100f;
            float _D1 = 100f;
            float _D2 = 100f;
            float _D3 = 100f;
            for(float currentE2Ratio = _E1Ratio[i]; currentE2Ratio <= 0.5f; currentE2Ratio += searchInterval)
            {
                float currentE2Degree = 180 * Mathf.Atan(currentE2Ratio * _W / _V) / Mathf.PI;
                float S1 = 1f;
                float S2 = (m * _E1Degree[i] + _omega0) / _omega;
                float S3 = (m * currentE2Degree + _omega0) / _omega;
                float D1 = 2 * _D * _V * Mathf.Tan(Mathf.PI * _E1Degree[i] / 180) / (S1 * _W);
                float D2 = 2 * _D * _V * Mathf.Tan(Mathf.PI * currentE2Degree / 180) / (S2 * _W);
                float D3 = _D / S3;
                float MidOutTotalFrameSize = (D2 * D2 + D3 * D3 * _alpha) * 4f / 1024f / 1024f;
                if(MidOutTotalFrameSize < MinimumTotalFrameSize)
                {
                    MinimumTotalFrameSize = MidOutTotalFrameSize;
                    optimalE2Degree = currentE2Degree;
                    optimalE2Ratio = currentE2Ratio;
                    _D1 = D1;
                    _D2 = D2;
                    _D3 = D3;
                }
            }
            ThreeLayersResults.Add(new ThreeLayers{optimalE2Ratio = optimalE2Ratio,
                                                optimalE2Degree = optimalE2Degree,
                                                _D1 = _D1,
                                                _D2 = _D2,
                                                _D3 = _D3,
                                                MinimumTotalFrameSize = MinimumTotalFrameSize});
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("optimalE2Ratio(Ratio),  optimalE2Degree(Degree),  D1,  D2,  D3,  MinimumTotalFrameSize(MB)");
        foreach (var item in ThreeLayersResults)
        {
            sb.AppendLine(item.ToString());
        }

        Console.WriteLine(sb.ToString());
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(
            Application.dataPath, "TwoOrThreeLayerSQVRResults", "ThreeLayerResults.txt"), 
            sb.ToString());
        Console.ReadLine();
    }

    public class TwoLayers
    {
        public TwoLayers()
        {
        }
        public float E1Ratio { get; set; }
        public float E1Degree { get; set; }
        public float _D1 { get; set; }
        public float _D2 { get; set; }
        public float remoteFrameSize { get; set; }
        public override string ToString()
        {
            return this.E1Ratio.ToString("F2") + "  " 
            + this.E1Degree.ToString("F2") + "  " 
            + this._D1.ToString("F4") + "  " 
            + this._D2.ToString("F4") + "  " 
            + this.remoteFrameSize.ToString("F3");
        }
    }

    public class ThreeLayers
    {
        public ThreeLayers()
        {
        }
        public float MinimumTotalFrameSize { get; set; }
        public float _D1 { get; set; }
        public float _D2 { get; set; }
        public float _D3 { get; set; }
        public float optimalE2Ratio { get; set; }
        public float optimalE2Degree { get; set; }
        public override string ToString()
        {
            return this.optimalE2Ratio.ToString("F3") + "  " 
                    + this.optimalE2Degree.ToString("F3") + "  " 
                    + this._D1.ToString("F4") + "  " 
                    + this._D2.ToString("F4") + "  " 
                    + this._D3.ToString("F4") + "  " 
                    + this.MinimumTotalFrameSize.ToString("F3");
        }
    }

    
}
