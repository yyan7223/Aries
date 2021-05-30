using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugUtils
{
    // RT alignment debug function
    int index1, index2 = 0;
    void saveRenderTexture(RenderTexture tex, bool remote)
    {
        RenderTexture.active = tex;
        Texture2D tex2D =
            new Texture2D(tex.width, tex.height, TextureFormat.RGB24, false);
        // false, meaning no need for mipmaps
        tex2D.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        RenderTexture.active = null;
        byte[] bytes = tex2D.EncodeToPNG();
        string path;
        if (remote)
        {
            path = string.Format("/remoteRT/remoteRT_{0}.png", index1);
        }
        else
        {
            path = string.Format("/foveaRT/foveaRT_{0}.png", index1);
        }
        File.WriteAllBytes(Application.dataPath + path, bytes);
        index1++;
        // Debug.Log("Successfully saved a texture");
    }

    void savefoveaRTBuffer(Texture2D tex)
    {
        // RenderTexture.active = tex;
        // Texture2D tex2D =
        //     new Texture2D(tex.width, tex.height, TextureFormat.RGB24, false);
        // // false, meaning no need for mipmaps
        // tex2D.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        // RenderTexture.active = null;
        byte[] bytes = tex.EncodeToPNG();
        string path = string.Format("/foveaRTBuffer/foveaRTbuffer_{0}.png", index2);
        File.WriteAllBytes(Application.dataPath + path, bytes);
        index2++;
        // Debug.Log("Successfully saved a texture");
    }

    void saveTexture2D(Texture2D tex)
    {
        byte[] bytes = tex.EncodeToPNG();
        string path = string.Format("/receivedRemoteRT/receivedRemoteRT_{0}.png", index1);
        File.WriteAllBytes(Application.dataPath + path, bytes);
        index1++;
        // Debug.Log("Successfully saved a texture");
    }

}
