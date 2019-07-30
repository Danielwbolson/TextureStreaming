using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This class manages streaming of information between the Unity Client and the SGCT Server
public class StreamingManager : MonoBehaviour {

    // Assuming 4 Cameras for 4 walls of cave
    int instances = 4;

    // References to our Cameras in scene
    Camera[] cameras;

    // Reference to our client which will interact with SGCT server
    UdpStreamingClient _udpStreamingClient;

    // Data from Server that is assumed to be a concatenated array of floats, row by row
    //byte[] viewMatricesBytes = null;
    float[] viewMatricesFloats = null;

    // View matrices created from data from server
    Matrix4x4[] viewMatrices;

    // Data necessary for rendering to a texture
    RenderTexture[] renderTextures;
    Texture2D[] textures;
    Rect[] rects;
    byte[][] pixels;

    // Start is called before the first frame update
    void Start() {

        // Initialize all variables and arrays
        Init();

    }

    // Update is called once per frame
    void Update() {

        // Update our view matrices with data from the SGCT server
        UpdateMatrices();

        // We now have our viewMatrices, time to render to textures and get the pixel data
        SceneToTexture();
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

        int width = (int)(1280 * (3.0f / 4.0f));
        int height = (int)(1440 * (3.0f / 4.0f));
        for (int i = 0; i < instances; i++) {
            textures[i] = new Texture2D(width, height, TextureFormat.RGB24, false);
            renderTextures[i] = new RenderTexture(width, height, 24);
            rects[i] = new Rect(0, 0, width, height);
            pixels[i] = new byte[width * height * 3]; // 4 channels
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

    void SceneToTexture() {

        // Run through everything and render each camera
        // setup variables if not done already
        for (int i = 0; i < instances; i++) {
            // Cache size of camera shot
            //int width = cameras[i].scaledPixelWidth;
            //int height = cameras[i].scaledPixelHeight;
            int width = (int)(1280 * (3.0f / 4.0f));
            int height = (int)(1440 * (3.0f / 4.0f));


            // Connect camera to renderTexture and render
            cameras[i].targetTexture = renderTextures[i];
            cameras[i].Render();
            RenderTexture.active = renderTextures[i];

            // Read in rendering to a texture
            textures[i].ReadPixels(rects[i], 0, 0);
            textures[i].Apply();

            // Clean-up
            RenderTexture.active = null;
            // Get pixel data
            Array.Copy(textures[i].GetRawTextureData(), pixels[i], pixels[i].Length);
        }
    }

    public byte[][] GetPixels() {
        return pixels;
    }
}