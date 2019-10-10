using System.IO;
using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;

public class TextureStreaming : MonoBehaviour
{
    int width = 256;
    int height = 256;
    
    public RenderTexture renderTex;
    Texture2D tex;
    Camera mainCamera;

    [DllImport("TextureStreamingC++")]
    static extern void SendTextures(IntPtr textureData, int size);

    [DllImport("TextureStreamingC++")]
    static extern void InitServer();

    [DllImport("TextureStreamingC++")]
    static extern void GetProjViewMatrices();

    // Start is called before the first frame update
    void Start()
    {
        mainCamera = Camera.main;
        tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Rect rectReadPicture = new Rect(0, 0, width, height);
        renderTex = new RenderTexture(256, 256, 32);

        mainCamera.targetTexture = renderTex;
        mainCamera.Render();
        RenderTexture.active = renderTex;

        tex.ReadPixels(rectReadPicture, 0, 0);
        tex.Apply();

        RenderTexture.active = null;

        byte[] pixels = tex.GetRawTextureData();

        GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        SendTextures(handle.AddrOfPinnedObject(), pixels.Length);
        handle.Free();
    }

    void Update() {

    }
}
