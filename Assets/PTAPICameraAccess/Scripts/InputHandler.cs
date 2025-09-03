using ARVis.Flatbuffers;
using JetBrains.Annotations;
using Meta.XR.EnvironmentDepth;
using PassthroughCameraSamples;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.UI;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.API;
using UnityEngine.XR.OpenXR.Features.Meta;
using static System.Net.Mime.MediaTypeNames;


public class InputHandler : MonoBehaviour
{
    public bool getDepth = false;
    public bool listenForButtonInput = true;
    public Texture2D errorTex;
    [HideInInspector]
    public WebCamTextureManager webCamTextureManager;
    [HideInInspector]
    public PassthroughCameraIntrinsics PCIntrins;
    [HideInInspector]
    public UnityEngine.Pose PCPose;

    [HideInInspector]
    public byte[] capturedImage = {0};
    [HideInInspector]
    public byte[] capturedDepth = { 0 };
    public RawImage image;
    [HideInInspector]
    public bool newImageFlag = false;
    [HideInInspector]
    [SerializeField] private EnvironmentDepthManager _environmentDepthManager;

    private static readonly int DepthTextureID = Shader.PropertyToID("_EnvironmentDepthTexture");
    private CommandBuffer _commandBuffer;

    public Shader m_DepthCopyShader;
    [HideInInspector]
    public string timestamp;

    [HideInInspector]
    public RenderTexture depthTex;

    public static InputHandler Instance;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Instance = this;
        webCamTextureManager = GameObject.FindAnyObjectByType<WebCamTextureManager>();
        PCIntrins = PassthroughCameraUtils.GetCameraIntrinsics(webCamTextureManager.Eye);
        PCPose = PassthroughCameraUtils.GetCameraPoseInWorld(webCamTextureManager.Eye);

        float widthScaleFactor = (float)380 / (float)240;

        if (image != null) image.rectTransform.localScale = new Vector3(image.rectTransform.localScale.x * widthScaleFactor, image.rectTransform.localScale.y, image.rectTransform.localScale.z);
        
        capturedImage = errorTex.GetRawTextureData();
        for (int i = 0; i < 10; i++)
        {
            Debug.Log(capturedImage[i]);
        }

        Texture2D colorTex = new Texture2D(256, 256, TextureFormat.ASTC_6x6, false);

        colorTex.LoadRawTextureData(capturedImage);
        colorTex.Apply();
        if (image != null) image.texture = colorTex;


        if (getDepth)
        {
            _commandBuffer = new CommandBuffer();
            depthTex = new RenderTexture(256, 256, GraphicsFormat.R16G16B16A16_UNorm, GraphicsFormat.None);



            var loader = XRGeneralSettings.Instance.Manager.activeLoader;
            var displaySubsystem = loader.GetLoadedSubsystem<XRDisplaySubsystem>();

            
            //_environmentDepthManager.onDepthTextureUpdate += (RenderTexture tex) => OnDepth(tex);
        }



    }



    // Update is called once per frame
    void Update()
    {
        if (OVRInput.Get(OVRInput.RawButton.A) && listenForButtonInput)
        {
            GetImage();
        }  
    }


    // ********************
    // This is the core function for capturing the image from the WebCamTextureManager and sending it to the server. All that's required is that the setup
    // was successful (i.e. the WebCamTextureManager is present and the dependencies) and you're good to go.
    // ********************
    public void GetImage()
    {
        if (webCamTextureManager.WebCamTexture != null)
        {
            PassthroughCameraIntrinsics intrinsics = PassthroughCameraUtils.GetCameraIntrinsics(webCamTextureManager.Eye);
            float widthScaleFactor = (float)intrinsics.Resolution.x / (float)intrinsics.Resolution.y;
            if (image != null) image.rectTransform.sizeDelta = new Vector2(intrinsics.Resolution.x, intrinsics.Resolution.y);

            Texture2D colorTex = new Texture2D(intrinsics.Resolution.x, intrinsics.Resolution.y, TextureFormat.RGBA32, false);

            capturedImage = ImageCollectionHelper.Color32ArrayToByteArray(webCamTextureManager.WebCamTexture.GetPixels32());

            colorTex.LoadRawTextureData(capturedImage);
            colorTex.Apply();
            if (image != null) image.texture = colorTex;

            if (getDepth)
            {
                Material m_DepthCopyMat = new Material(m_DepthCopyShader);
                //m_DepthCopyMat.SetTexture("_MyDepthTex", tex);
                //Graphics.Blit(tex, depthTex, m_DepthCopyMat);
                //testCube.GetComponent<MeshRenderer>().material.SetTexture("Name", depthTex);

                m_DepthCopyMat.SetTexture(DepthTextureID, depthTex);
                _commandBuffer.SetRenderTarget(new RenderTargetIdentifier(depthTex, 0, CubemapFace.Unknown, RenderTargetIdentifier.AllDepthSlices),
                    colorLoadAction: RenderBufferLoadAction.DontCare, colorStoreAction: RenderBufferStoreAction.Store,
                    depthLoadAction: RenderBufferLoadAction.DontCare, depthStoreAction: RenderBufferStoreAction.DontCare);
                _commandBuffer.DrawProcedural(Matrix4x4.identity, m_DepthCopyMat, 0, MeshTopology.Triangles, 3, 2);
                Graphics.ExecuteCommandBuffer(_commandBuffer);
                _commandBuffer.Clear();


                //Debug.Log("Width: " + depthTex.width + "    Height: " + depthTex.height);
                //Debug.Log("Color buffer: " + depthTex.colorBuffer + "   Depth buffer: " + depthTex.depthBuffer);
                //Debug.Log("Graphics Format: " + depthTex.graphicsFormat + "     Depth/Stencil: " + depthTex.depthStencilFormat + "      Stencil Format: " + depthTex.stencilFormat);


                Texture2D depthTexture2D = new Texture2D(depthTex.width, depthTex.height, TextureFormat.RGBAHalf, false);
                RenderTexture.active = depthTex;
                depthTexture2D.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0);
                depthTexture2D.Apply();
                //image.texture = depthTexture2D;
                byte[] depthBytes = depthTexture2D.GetRawTextureData();
                //for (int i = 0; i < 10; i++)
                //{
                //    Debug.Log(depthBytes[i]);
                //}

            }

        }
        else
        {
            if (image != null) image.texture = errorTex;
            capturedImage = errorTex.GetRawTextureData();
        }
        newImageFlag = true;
        timestamp = DateTime.Now.ToString("HHmmss");
    }

    private void FixedUpdate()
    {

    }

}

public class ImageCollectionHelper
{
    public static byte[] Color32ArrayToByteArray(Color32[] colors)
    {
        if (colors == null || colors.Length == 0)
            return null;

        int lengthOfColor32 = Marshal.SizeOf(typeof(Color32));
        int length = lengthOfColor32 * colors.Length;
        byte[] bytes = new byte[length];

        GCHandle handle = default(GCHandle);
        try
        {
            handle = GCHandle.Alloc(colors, GCHandleType.Pinned);
            IntPtr ptr = handle.AddrOfPinnedObject();
            Marshal.Copy(ptr, bytes, 0, length);
        }
        finally
        {
            if (handle != default(GCHandle))
                handle.Free();
        }

        return bytes;
    }
}


