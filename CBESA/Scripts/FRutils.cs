using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FRutils
{   
    Vector4 defaultClipPlaneRLTB; // default ClipPlane Right Left Top Bottom

    public FRutils(Camera cam)
    {
        // compute default defaultClipPlaneRLTB
        defaultClipPlaneRLTB = ComputeDefaultclipPlaneRLTB(cam);
    }

    // default ClipPlane Right Left Top Bottom
    /**
    * @description: Get the Default clip Plane RLTB which is responsible for rendering the whole screen 
    *               It is the default clip plane after initializaing a camera
    *               We will then use it to compute the clipPlane coordinate of foveated part during runnting time
    * @param {Camera cam} 
    * @return {Vector4 defaultClipPlaneRLTB} 
    */
    public Vector4 ComputeDefaultclipPlaneRLTB(Camera cam)
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

    /**
    * @description: Compute the right, left, top, bottom of fovea part or mid part
                    on the clip plane according to different eccentricity.
    * @param {Vector2 fovea_coordinate, float eccentricity, Vector2 foveaInClipPlane, Vector4 defaultClipPlaneRLTB} 
    * @return {Vector4 clipPlaneRLTB} 
    */
    public Vector4 ComputeClipPlaneRLTB(Vector2 fovea_coordinate, float eccentricity, Vector2 foveaInClipPlane, Vector4 defaultClipPlaneRLTB)
    {
        // if larger than border, then set the value equals to border
        // if smaller than border, compute the value normally
        float right = (fovea_coordinate.x + eccentricity >= 1.0f) ? defaultClipPlaneRLTB.x : (foveaInClipPlane.x + eccentricity * (defaultClipPlaneRLTB.x - defaultClipPlaneRLTB.y));
        float left = (fovea_coordinate.x - eccentricity <= 0.0f) ? defaultClipPlaneRLTB.y : (foveaInClipPlane.x - eccentricity * (defaultClipPlaneRLTB.x - defaultClipPlaneRLTB.y));
        float top = (fovea_coordinate.y + eccentricity >= 1.0f) ? defaultClipPlaneRLTB.z : (foveaInClipPlane.y + eccentricity * (defaultClipPlaneRLTB.z - defaultClipPlaneRLTB.w));
        float bottom = (fovea_coordinate.y - eccentricity <= 0.0f) ? defaultClipPlaneRLTB.w : (foveaInClipPlane.y - eccentricity * (defaultClipPlaneRLTB.z - defaultClipPlaneRLTB.w));
        return new Vector4(right, left, top, bottom);
    }

    /**
    * @description: Compute and refresh the field of view (FOV) of three sub cameras according to different eccentricity.
    * @param {Camera cam, float eccentricity}: Camera and eccentricity
    * @return {void} 
    */
    void RefreshFOV(Camera cam, float eccentricity, Transform trans)
    {
        // Reset the fieldOfView to default value, important! 
        // because the computation of FOV in this frame is based on the FOV of the last frame.
        cam.fieldOfView = 60.0f;

        //Compute the normalized top coordinate for fovea part, mid part, and out part
        // normalized center coordinate(0.5f, 0.5f)
        Vector2 top_coordinate;
        top_coordinate.x = 0.5f;
        top_coordinate.y = 0.5f + eccentricity;

        //get the virtual world position of foveat point and top point on the far clip plane
        Vector3 center_vworldPos; // fovea coordinate map to virtual world position
        float disCenter2cam; // the distance between fovea_worldPos and the position of the sub camera
        center_vworldPos = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, cam.farClipPlane));
        disCenter2cam = Vector3.Distance(center_vworldPos, trans.position);

        // top coordinate map to virtual world position
        Vector3 top_vworldPos = cam.ViewportToWorldPoint(new Vector3(top_coordinate.x, top_coordinate.y, cam.farClipPlane));
        // the distance between top_worldPos and the position of the sub camera
        float disTop2cam = Vector3.Distance(top_vworldPos, trans.position);

        //Compute FOV
        //the results returned by Mathf.Acos is the angle in radians, we need to multiple 180 degree to turn it to the angle
        cam.fieldOfView = 2 * 180 * Mathf.Acos(disCenter2cam / disTop2cam) / Mathf.PI;
    }

    /**
    * @description: Compute the Projection Matrix of cam_fovea or cam_mid according to different eccentricity.
                    Use the defaultClipPlaneRLTB and eccentricity to compute the ClipPlaneRLTB of cam_fovea and cam_mid
                    Than take RLTB into the formula below to calculate the PM of cam_fovea and cam_mid

                    The formula of projection matrix: 
                        | 2n/(r-l)    0           (r+l)/(r-l)        0          |
                        | 0           2n/(t-b)    (t+b)/(t-b)        0          |
                        | 0           0           -(f+n)/(f-n)       -2fn/(f-n) |
                        | 0           0           -1                 0          |

    * @param {Camera cam, Vector2 fovea_coordinate, float eccentricity} 
    * @return {void} 
    */
    public void RefreshPM(Camera cam, Vector2 fovea_coordinate, float eccentricity)
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

    /**
    * @description: Refresh the shader parameters
    * @param {Material mat, Texture foveaRT, Texture MidRT, Texture OutRT, Vector2 fovea_coordinate, float eccentricity1, float eccentricity2} 
    * @return {void} 
    */
    public void RefreshShaderParameter(Material mat, Texture foveaRT, Texture remoteRT, Vector2 fovea_coordinate, float eccentricity1, float eccentricity2)
    {
        mat.SetTexture("_MainTex", remoteRT);
        mat.SetTexture("_FoveaTex", foveaRT);
        mat.SetFloat("_FoveaCoordinateX", fovea_coordinate.x);
        mat.SetFloat("_FoveaCoordinateY", fovea_coordinate.y);
        mat.SetFloat("_E1", eccentricity1);
        mat.SetFloat("_E2", eccentricity2);
    }
}
