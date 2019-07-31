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


    /***** * * * * * SERVER VARS * * * * * ******/
    // Where the server is running
    private string registerClientUrl = "http://localhost:5000";
    private IPAddress client = IPAddress.Parse("192.168.2.80");
    private const int TRACKING_PORT = 5001;

    private UdpClient listener;
    private IPEndPoint groupEP;

    private Thread receiverThread;
    private bool threadRunning = false;


    /***** * * * * * BUFFER/MEMORY VARS * * * * * *****/
    // Raw bytes from SGCt
    private const int MATRIX_BUFFER_LEN = 256;
    private byte[] rawBufferData = new byte[MATRIX_BUFFER_LEN];
    // Turn bytes into float array
    private float[] floatBufferData = new float[MATRIX_BUFFER_LEN / sizeof(float)];
    // Return identity if things haven't finished yet
    private readonly Matrix4x4[] identityMatrixBufferData = {
        new Matrix4x4(new Vector4(1,0,0,0), new Vector4(0,1,0,0), new Vector4(0,0,1,0), new Vector4(0,0,0,1)),
        new Matrix4x4(new Vector4(1,0,0,0), new Vector4(0,1,0,0), new Vector4(0,0,1,0), new Vector4(0,0,0,1)),
        new Matrix4x4(new Vector4(1,0,0,0), new Vector4(0,1,0,0), new Vector4(0,0,1,0), new Vector4(0,0,0,1)),
        new Matrix4x4(new Vector4(1,0,0,0), new Vector4(0,1,0,0), new Vector4(0,0,1,0), new Vector4(0,0,0,1)),
    };
    // return camera matrices
    private Matrix4x4[] matrixBufferData = new Matrix4x4[MATRIX_BUFFER_LEN / sizeof(float) / 16]; // 16 is size of matrices

    // Pixel buffers to send to sgct
    private const int PIXEL_DATA_LEN = 7896 * 4; //64 * 64 * 3 * 4; // 960 * 1080 * 3 * 4; // width, height, channels, screens
    private byte[] rawPixelData = new byte[PIXEL_DATA_LEN];


    void Start() {
        StartCoroutine(RegisterServer(true));

        this.listener = new UdpClient(UdpStreamingClient.TRACKING_PORT);
        this.groupEP = new IPEndPoint(this.client, UdpStreamingClient.TRACKING_PORT);
        this.receiverThread = new Thread(this.TrackingReceiveThread);
        this.receiverThread.Start();

        manager = GameObject.Find("StreamingManager").GetComponent<StreamingManager>();
    }

    void OnDisable() {
        listener.Close();
        this.threadRunning = false;
        this.receiverThread.Join();
    }

    void TrackingReceiveThread() {
        this.threadRunning = true;
        while (threadRunning) {

            SocketAsyncEventArgs e = new SocketAsyncEventArgs();
            e.Completed += new EventHandler<SocketAsyncEventArgs>(this.ReceiveUdpData);
            e.SetBuffer(rawBufferData, 0, MATRIX_BUFFER_LEN);
            this.listener.Client.ReceiveAsync(e);

            SocketAsyncEventArgs f = new SocketAsyncEventArgs();
            f.Completed += new EventHandler<SocketAsyncEventArgs>(this.SendTextureDataToServer);
            f.SetBuffer(rawPixelData, 0, PIXEL_DATA_LEN);
            f.RemoteEndPoint = groupEP;
            this.listener.Client.SendToAsync(f);

            // Run this loop at 100fps
            Thread.Sleep(1);
        }
        Debug.Log("Finished Thread");
    }

    void ReceiveUdpData(object sender, SocketAsyncEventArgs e) {

        Buffer.BlockCopy(e.Buffer, 0, floatBufferData, 0, e.Buffer.Length);

        // Turn floats into matrices
        for (int i = 0; i < matrixBufferData.Length; i++) { // Run through each matrix
            int start = i * 16;
            for (int r = 0; r < 4; r++) {
                int offset = 4 * r;

                float[] floatRow = util.Utility.SubArray<float>(floatBufferData, start + offset, 4);
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
    void SendTextureDataToServer(object sender, SocketAsyncEventArgs f) {
        // Get our data as a byte[][]
        byte[][] p = manager.GetPixels();
        int length = 0;
        for (int i = 0; i < p.Length; i++) {
            length += p[i].Length;
        }

        // Change data into a byte[]
        Buffer.BlockCopy(p.SelectMany(a => a).ToArray(), 0, rawPixelData, 0, length);
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