using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveLRHRimage : MonoBehaviour
{
    public Camera cam;

    private RenderTexture LR;
    private float LRscale = 0.25f;

    private RenderTexture MR;
    private float MRscale = 0.5f;

    private RenderTexture HR; 
    private float HRscale = 1.0f;

    private RenderTexture LR_0_03;
    private RenderTexture LR_0_04;
    private RenderTexture LR_0_05;
    private RenderTexture LR_0_07;
    private RenderTexture LR_0_09;
    private RenderTexture LR_0_11;

    private float frameCount = 0;

    // Start is called before the first frame update
    void Start()
    {
        LR_0_03 = new RenderTexture(2119, 953, 16, RenderTextureFormat.ARGB32);
        LR_0_04 = new RenderTexture(1639, 738, 16, RenderTextureFormat.ARGB32);
        LR_0_05 = new RenderTexture(1339, 603, 16, RenderTextureFormat.ARGB32);
        LR_0_07 = new RenderTexture(985, 443, 16, RenderTextureFormat.ARGB32);
        LR_0_09 = new RenderTexture(784, 353, 16, RenderTextureFormat.ARGB32);
        LR_0_11 = new RenderTexture(655, 295, 16, RenderTextureFormat.ARGB32);

        // Creat the render texture
        LR = new RenderTexture(
            (int)(UnityEngine.Screen.width * LRscale),
            (int)(UnityEngine.Screen.height * LRscale),
            16, RenderTextureFormat.ARGB32
        );

        MR = new RenderTexture(
            (int)(UnityEngine.Screen.width * MRscale),
            (int)(UnityEngine.Screen.height * MRscale),
            16, RenderTextureFormat.ARGB32
        );

        // fix the resolution
        HR = new RenderTexture(
                    (int)(UnityEngine.Screen.width * HRscale),
                    (int)(UnityEngine.Screen.height * HRscale),
                    16, RenderTextureFormat.ARGB32);
    }

    // Update is called once per frame
    void Update()
    {
        if(frameCount == 0)
        {
            cam.targetTexture = LR;
            LR.Release();
            cam.Render();
        }
        else if(frameCount == 1)
        {
            cam.targetTexture = MR;
            MR.Release();
            cam.Render();
        }
        else if(frameCount == 2)
        {
            cam.targetTexture = HR;
            HR.Release();
            cam.Render();
        }
        else if(frameCount == 3)
        {
            cam.targetTexture = LR_0_03;
            MR.Release();
            cam.Render();
        }
        else if(frameCount == 4)
        {
            cam.targetTexture = LR_0_04;
            HR.Release();
            cam.Render();
        }
        else if(frameCount == 5)
        {
            cam.targetTexture = LR_0_05;
            MR.Release();
            cam.Render();
        }
        else if(frameCount == 6)
        {
            cam.targetTexture = LR_0_07;
            HR.Release();
            cam.Render();
        }
        else if(frameCount == 7)
        {
            cam.targetTexture = LR_0_09;
            MR.Release();
            cam.Render();
        }
        else if(frameCount == 8)
        {
            cam.targetTexture = LR_0_11;
            HR.Release();
            cam.Render();
        }
        else
        {
            Application.Quit();
        }
        frameCount++;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if(frameCount == 0)
        {
            saveRenderTexture(LR, "LR");
        }
        else if(frameCount == 1)
        {
            saveRenderTexture(MR, "MR");
        }
        else if(frameCount == 2)
        {
            saveRenderTexture(HR, "HR");
        }
        else if(frameCount == 3)
        {
            saveRenderTexture(LR_0_03, "LR_0_03");
        }
        else if(frameCount == 4)
        {
            saveRenderTexture(LR_0_04, "LR_0_04");
        }
        else if(frameCount == 5)
        {
            saveRenderTexture(LR_0_05, "LR_0_05");
        }
        else if(frameCount == 6)
        {
            saveRenderTexture(LR_0_07, "LR_0_07");
        }
        else if(frameCount == 7)
        {
            saveRenderTexture(LR_0_09, "LR_0_09");
        }
        else if(frameCount == 8)
        {
            saveRenderTexture(LR_0_11, "LR_0_11");
        }
    }

    void saveRenderTexture(RenderTexture tex, string name)
    {
        RenderTexture.active = tex;
        Texture2D tex2D =
            new Texture2D(tex.width, tex.height, TextureFormat.RGB24, false);
        // false, meaning no need for mipmaps
        tex2D.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        RenderTexture.active = null;
        byte[] bytes = tex2D.EncodeToPNG();
        string path = string.Format("VikingVillage_{0}.png", name);;
        File.WriteAllBytes(Application.dataPath + path, bytes);
        Debug.Log("Successfully saved a texture");
    }
}
