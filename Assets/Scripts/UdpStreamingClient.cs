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
using System.Text;
using System.Threading;

public class UdpStreamingClient : MonoBehaviour {
    public bool dataFlag = false;

    // Where the server is running
    private string registerClientUrl = "http://localhost:5000";
    private IPAddress client = IPAddress.Parse("0.0.0.0");
    private const int TRACKING_PORT = 5001;

    private UdpClient listener;
    private IPEndPoint groupEP;

    private const int MATRIX_BUFFER_LEN = 256;
    private byte[] matrixBufferData = new byte[MATRIX_BUFFER_LEN];

    private float[] floatBufferData = new float[MATRIX_BUFFER_LEN / 4];
    private readonly float[] identifyBufferData = {
        1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1,
        1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1,
        1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1,
        1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1
    };

    private Thread receiverThread;
    private bool threadRunning = false;

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


    void Start() {
        StartCoroutine(RegisterServer(true));

        this.listener = new UdpClient(UdpStreamingClient.TRACKING_PORT);
        this.groupEP = new IPEndPoint(this.client, UdpStreamingClient.TRACKING_PORT);
        this.receiverThread = new Thread(this.TrackingReceiveThread);
        this.receiverThread.Start();
    }

    private void Update() {
        //byte[] receiveBytes = this.listener.Receive(ref this.groupEP);
        //SocketAsyncEventArgs e = new SocketAsyncEventArgs();
        //e.Completed += new EventHandler<SocketAsyncEventArgs>(this.ReceiveUdpData);
        //Array.Clear(this.matrixBufferData, 0, MATRIX_BUFFER_LEN);
        //e.SetBuffer(matrixBufferData, 0, MATRIX_BUFFER_LEN);
        //this.listener.Client.ReceiveAsync(e);

        //Buffer.BlockCopy(receiveBytes, 0, floatBufferData, 0, receiveBytes.Length);
        //if (dataFlag == false) {
        //    dataFlag = true;
        //}
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
            e.SetBuffer(matrixBufferData, 0, MATRIX_BUFFER_LEN);
            this.listener.Client.ReceiveAsync(e);

            // Run this loop at 100fps
            Thread.Sleep(1);
        }
        Debug.Log("Finished Thread");
    }

    void ReceiveUdpData(object sender, SocketAsyncEventArgs e) {
        //string s = "";
        //string q = "";
        //foreach (byte b in e.Buffer) {
        //    s += string.Format("{0:X} ", b);
        //    q += (char)b;
        //}

        Buffer.BlockCopy(e.Buffer, 0, floatBufferData, 0, e.Buffer.Length);
        //Debug.Log(s);
        //Debug.Log(q);
        //System.IO.File.WriteAllBytes("dummyfile.txt", e.Buffer);

        if (dataFlag == false) {
            dataFlag = true;
        }
    }

    public float[] GetMatrixDataFromServer() {
        if (!dataFlag) {
            return identifyBufferData;
        }
        return floatBufferData;

    }
}
