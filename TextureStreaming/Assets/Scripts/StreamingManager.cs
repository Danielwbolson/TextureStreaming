using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// This class manages streaming of information between the Unity Client and the SGCT Server
public class StreamingManager : MonoBehaviour {

    // Assuming 4 Cameras for 4 walls of cave
    int instances = 4;

    // References to our Cameras in scene
    Camera[] cameras;

    // Reference to our client which will interact with SGCT server
    UdpStreamingClient _udpStreamingClient;

    // View matrices created from data from server
    Matrix4x4[] viewMatrices;

    // Data necessary for rendering to a texture
    RenderTexture[] renderTextures;
    Texture2D[] textures;
    Rect[] rects;
    byte[][] pixels;

    int width = 1280 / 2;  // (int)(1280 * (3.0f / 4.0f)); // for cave w/upsampling
    int height = 1440 / 2; // (int)(1440 * (3.0f / 4.0f)); // for cave w/upsampling

    // Start is called before the first frame update
    void Start() {

        // Initialize all variables and arrays
        Init();
        
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120;

    }

    // Update is called once per frame
    void Update() {

        // Update our view matrices with data from the SGCT server
        UpdateMatrices();

        // We now have our viewMatrices, time to render to textures and get the pixel data
        StartCoroutine(SceneToTexture());

        GC.Collect();
    }

    void Init() {
        // Get our client connection to server on SGCT
        _udpStreamingClient = gameObject.AddComponent<UdpStreamingClient>();

        // Allocate our view matrices
        viewMatrices = new Matrix4x4[instances];
        for (int i = 0; i < instances; i++) {
            viewMatrices[i] = Matrix4x4.identity;
        }

        // Create and allocate our cameras
        // One per gameObject
        GameObject[] cameraObjects = new GameObject[instances];
        cameras = new Camera[instances];
        for (int i = 0; i < instances; i++) {
            cameraObjects[i] = new GameObject("Camera: " + i.ToString());
            cameras[i] = cameraObjects[i].AddComponent<Camera>();
            cameras[i].worldToCameraMatrix = viewMatrices[i];
        }

        // Set up RenderTexture data
        renderTextures = new RenderTexture[instances];
        textures = new Texture2D[instances];
        rects = new Rect[instances];
        pixels = new byte[instances][];

        for (int i = 0; i < instances; i++) {
            textures[i] = new Texture2D(width, height, TextureFormat.RGB24, false);
            renderTextures[i] = new RenderTexture(width, height, 24);
            rects[i] = new Rect(0, 0, width, height);
            pixels[i] = new byte[width * height * 3]; // 3 channels
            //pixels[i] = new byte[7896]; //jpg with 75% quality
        }
    }

    void UpdateMatrices() {

        // Grab our view matrices from our Server
        viewMatrices = _udpStreamingClient.GetMatrixDataFromServer();

        // Update Camera
        for (int i = 0; i < viewMatrices.Length; i++) {
            cameras[i].worldToCameraMatrix = viewMatrices[i];
        }
    }

    IEnumerator SceneToTexture() {

        // Run through everything and render each camera
        // setup variables if not done already
        for (int i = 0; i < instances; i++) {
            // Connect camera to renderTexture and render
            cameras[i].targetTexture = renderTextures[i];
            cameras[i].Render();
            RenderTexture.active = renderTextures[i];

            // Asynchrously request rendertexture data from GPU
            UnityEngine.Rendering.AsyncGPUReadbackRequest request = UnityEngine.Rendering.AsyncGPUReadback.Request(renderTextures[i], 0);
            while (!request.done) {
                yield return new WaitForEndOfFrame();
            }
            pixels[i] = request.GetData<byte>().ToArray();

            // We want to get our pixel data in png form, which requires creating a new texture2D
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.LoadImage(pixels[i]);
            pixels[i] = tex.EncodeToPNG();

            // Clean-up
            RenderTexture.active = null;
        }
    }

    public void GetPixels(out byte[][] p) {
        // Copy over so we don't have a reference
        p = new byte[instances][];
        for (int i = 0; i < instances; i++) {
            p[i] = pixels[i].Select(a => a).ToArray();
        }
    }
}