/*
 * Inspiration: https://social.msdn.microsoft.com/Forums/en-US/92846ccb-fad3-469a-baf7-bb153ce2d82b/simple-udp-example-code
 */

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Text;
using System.Threading;

public class UdpStreamingClient : MonoBehaviour {
    public bool dataFlag = false;

    // Referene to manager in scene
    StreamingManager manager;
    const int instances = 4;


    /***** * * * * * SERVER VARS * * * * * ******/
    // Where the server is running
    private string registerClientUrl = "http://localhost:5000";
    private IPAddress client = IPAddress.Parse("192.168.2.80");
    private const int MATRIX_PORT = 5001;
    private const int TEXTURE_PORT = 5002;

    private UdpClient matrixListener;
    private UdpClient textureSender;
    private IPEndPoint matrixEP;
    private IPEndPoint textureEP;

    private Thread receiverThread;
    private bool threadRunning = false;
    private object mutex;


    /***** * * * * * BUFFER/MEMORY VARS * * * * * *****/
    // Raw bytes from SGCt
    private const int MATRIX_BUFFER_LEN = 256;
    private byte[] rawMatrixData = new byte[MATRIX_BUFFER_LEN];
    // Turn bytes into float array
    private float[] floatMatrixData = new float[MATRIX_BUFFER_LEN / sizeof(float)];
    // Return identity if things haven't finished yet
    private readonly Matrix4x4[] identityMatrixBufferData = {
        new Matrix4x4(new Vector4(1,0,0,0), new Vector4(0,1,0,0), new Vector4(0,0,-1,0), new Vector4(0,0,0,1)),
        new Matrix4x4(new Vector4(1,0,0,0), new Vector4(0,1,0,0), new Vector4(0,0,-1,0), new Vector4(0,0,0,1)),
        new Matrix4x4(new Vector4(1,0,0,0), new Vector4(0,1,0,0), new Vector4(0,0,-1,0), new Vector4(0,0,0,1)),
        new Matrix4x4(new Vector4(1,0,0,0), new Vector4(0,1,0,0), new Vector4(0,0,-1,0), new Vector4(0,0,0,1)),
    };
    // return camera matrices
    private Matrix4x4[] matrixBufferData = new Matrix4x4[MATRIX_BUFFER_LEN / sizeof(float) / 16]; // 16 is size of matrices

    // Pixel buffers to send to sgct
    private int chunkLength = 5000;
    private const int initNumChunks = 10;
    private int[] numChunks;
    private byte[][] rawPixelData;
    private int[] rawPixelLength;
    private byte[][][] rawPixelChunks;
    private byte[][][] finalPixelChunks;
    //private int[][] rawPixelChunkLengths;


    void Start() {
        StartCoroutine(RegisterServer(true));

        // Init matrix port/server
        this.matrixListener = new UdpClient(UdpStreamingClient.MATRIX_PORT);
        //this.matrixEP = new IPEndPoint(this.client, UdpStreamingClient.MATRIX_PORT);
        this.matrixEP = new IPEndPoint(this.client, UdpStreamingClient.MATRIX_PORT);

        // Init texture port/server
        this.textureSender = new UdpClient(UdpStreamingClient.TEXTURE_PORT);
        this.textureEP = new IPEndPoint(this.client, UdpStreamingClient.TEXTURE_PORT);

        // Init secondary thread
        this.receiverThread = new Thread(this.TrackingReceiveThread);
        this.receiverThread.Start();

        manager = GameObject.Find("StreamingManager").GetComponent<StreamingManager>();
        mutex = new object();

        // Init of pixel data to avoid errors on pixel data
        rawPixelData = new byte[instances][];
        rawPixelLength = new int[instances];
        rawPixelChunks = new byte[instances][][];
        finalPixelChunks = new byte[instances][][];
        //rawPixelChunkLengths = new int[instances][];
        numChunks = new int[instances];
        for (int i = 0; i < instances; i++) {
            rawPixelData[i] = new byte[chunkLength];
            rawPixelLength[i] = chunkLength;
            rawPixelChunks[i] = new byte[initNumChunks][];
            finalPixelChunks[i] = new byte[initNumChunks][];
            //rawPixelChunkLengths[i] = new int[initNumChunks];
            numChunks[i] = initNumChunks;
            for (int j = 0; j < initNumChunks; j++) {
                rawPixelChunks[i][j] = new byte[chunkLength];
                finalPixelChunks[i][j] = new byte[chunkLength];
                //rawPixelChunkLengths[i][j] = chunkLength;
            }
        }
    }

    void OnDisable() {
        matrixListener.Close();
        textureSender.Close();
        this.threadRunning = false;
        this.receiverThread.Join();
    }

    void TrackingReceiveThread() {
        this.threadRunning = true;
        while (threadRunning) {

            SocketAsyncEventArgs e = new SocketAsyncEventArgs();
            e.Completed += new EventHandler<SocketAsyncEventArgs>(this.ReceiveUdpData);
            e.SetBuffer(rawMatrixData, 0, MATRIX_BUFFER_LEN);
            e.RemoteEndPoint = matrixEP;
            this.matrixListener.Client.ReceiveAsync(e);

            lock (mutex) {
                PopulateBuffers();
            }

            for (int i = 0; i < rawPixelChunks.Length; i++) {
                for (int j = 0; j < rawPixelChunks[i].Length; j++) {
                    SocketAsyncEventArgs f = new SocketAsyncEventArgs();
                    f.RemoteEndPoint = textureEP;
                    f.SetBuffer(finalPixelChunks[i][j], 0, finalPixelChunks[i][j].Length);
                    this.textureSender.Client.SendToAsync(f);
                }
            }

            // Run this loop at 100fps
            Thread.Sleep(1);
        }
        Debug.Log("Finished Thread");
    }

    void ReceiveUdpData(object sender, SocketAsyncEventArgs e) {

        Buffer.BlockCopy(e.Buffer, 0, floatMatrixData, 0, e.Buffer.Length);

        // Turn floats into matrices
        for (int i = 0; i < matrixBufferData.Length; i++) { // Run through each matrix
            int start = i * 16;
            for (int r = 0; r < 4; r++) {
                int offset = 4 * r;

                float[] floatRow = util.Utility.SubArray<float>(floatMatrixData, start + offset, 4);
                Vector4 row;
                if (r != 2) { // Unity camera space follows -z is forward, so must negate 3rd row
                    row = new Vector4(floatRow[0], floatRow[1], floatRow[2], floatRow[3]);
                } else {
                    row = new Vector4(-floatRow[0], -floatRow[1], -floatRow[2], -floatRow[3]);
                }
                matrixBufferData[i].SetRow(r, row);
            }
        }

        if (dataFlag == false) {
            dataFlag = true;
        }
    }

    // Called by asynchronous events to send data to server
    void PopulateBuffers() {

        // Get our data as a byte[][] and each byte buffer's length in pixels
        manager.GetPixels(out rawPixelData, out rawPixelLength);

        // Break our data into chunks
        // which instance, which chunk is this, total chunks, how long is the buffer, lastpixelIndex
        const int metaIntPieces = 5;
        int metaOffset = sizeof(int) * metaIntPieces;

        // Offset for our metadata
        int actualPixelChunkSize = chunkLength - metaOffset;

        for (int i = 0; i < rawPixelLength.Length; i++) {

            // Get how many chunks we are going to need for each image
            numChunks[i] = Mathf.CeilToInt((float)rawPixelLength[i] / (float)actualPixelChunkSize);

            // Re-init our chunk array to the correct number of chunks
            rawPixelChunks[i] = new byte[numChunks[i]][];
            finalPixelChunks[i] = new byte[numChunks[i]][];

            for (int j = 0; j < numChunks[i]; j++) {
                // Fill our chunkBuffer with chunks of size actualPixelChunkSize (save room for metadata) from the raw data
                // IF we do not have enough to fill the chunk size, grab the rest of the bytes
                int lengthOfSubarray = Mathf.Min(actualPixelChunkSize, rawPixelLength[i] - (j * actualPixelChunkSize));
                rawPixelChunks[i][j] = util.Utility.SubArray<byte>(rawPixelData[i], j * actualPixelChunkSize, lengthOfSubarray);

                // Get our metadata
                int[] metaIntData = new int[metaIntPieces] {
                    i, j, numChunks[i], chunkLength, lengthOfSubarray
                };
                byte[] metaData = new byte[metaOffset];
                Buffer.BlockCopy(metaIntData.ToArray(), 0, metaData, 0, metaOffset);

                // Combine metadata and our pixel data into one buffer
                List<byte> finalPixelChunkList = new List<byte>();
                finalPixelChunkList.AddRange(metaData);
                finalPixelChunkList.AddRange(rawPixelChunks[i][j]);
                finalPixelChunks[i][j] = finalPixelChunkList.ToArray();
            }
        }
    }

    // Allows StreamingManager access to the matrix data
    public Matrix4x4[] GetMatrixDataFromServer() {
        if (!dataFlag) {
            return identityMatrixBufferData;
        }
        return matrixBufferData;
    }

    // https://stackoverflow.com/a/6803109
    public static string GetLocalIPAddress() {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList) {
            if (ip.AddressFamily == AddressFamily.InterNetwork) {
                return ip.ToString();
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }

    IEnumerator RegisterServer(bool register) {
        Debug.Log("Registering with server");
        string query;
        if (register) {
            query = String.Format("{0}/register?ip={1}", this.registerClientUrl, GetLocalIPAddress());
        } else {
            query = String.Format("{0}/unregister", this.registerClientUrl);
        }
        UnityWebRequest uwr = UnityWebRequest.Get(query);
        yield return uwr.SendWebRequest();


        if (!uwr.isNetworkError) {
            Debug.Log("Success!");
            Debug.Log(uwr.downloadHandler.text);
        } else {
            Debug.LogError("(Tracking) Error While Registering: " + uwr.error);
        }
    }

}