using ARVis.Flatbuffers;
using AsyncIO;
using Google.FlatBuffers;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Concurrent;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Receiver
{
    private readonly Thread receiveThread;
    private bool running;
    private InputHandler inputHandler;
    public string serverIP = "10.214.155.24";


    public Receiver()
    {
        receiveThread = new Thread((object callback) =>
        {
            using (var socket = new RequestSocket())
            { 

                socket.Connect("tcp://" + serverIP + ":5555");
                //socket.Connect("tcp://localhost:5555");
                inputHandler = InputHandler.Instance;
                

                Debug.Log("====== Networking: Reciever Init");

                while (running)
                {
                    if (inputHandler.newImageFlag == true)
                    {
                        FlatBufferBuilder builder = new FlatBufferBuilder(1024);
                        BuildFrame(builder);
                        socket.SendFrame(builder.SizedByteArray());
                        inputHandler.newImageFlag = false;
                        Debug.Log("Sent new image");
                    }
                    else
                    {
                        Debug.Log("Empty frame send");
                        socket.SendFrameEmpty();
                    }
                    byte[] message = socket.ReceiveFrameBytes();
                    ((Action<byte[]>)callback)(message);
                }
            }
        });
    }

    public void BuildFrame(FlatBufferBuilder builder)
    {
        Debug.Log("====== Networking: Frame Build started");

        
        
        // use of shorts prevents resolutions above 32k in one dimension. Shouldn't be an issue on Quest 3
        
        Intrinsics.StartIntrinsics(builder);
        Offset<Vec2> focallengthvals = Vec2.CreateVec2(builder, inputHandler.PCIntrins.FocalLength.x, inputHandler.PCIntrins.FocalLength.y);
        Intrinsics.AddFocallength(builder, focallengthvals);
        Offset<Vec2> princpointvals = Vec2.CreateVec2(builder, inputHandler.PCIntrins.PrincipalPoint.x, inputHandler.PCIntrins.PrincipalPoint.y);
        Intrinsics.AddPrincipalpoint(builder, princpointvals);
        Offset<Vec2Int> resvals = Vec2Int.CreateVec2Int(builder, (short)inputHandler.PCIntrins.Resolution.x, (short)inputHandler.PCIntrins.Resolution.y);
        Intrinsics.AddResolution(builder, resvals);
        Intrinsics.AddSkew(builder, inputHandler.PCIntrins.Skew);
        Offset<Intrinsics> intrinsics = Intrinsics.EndIntrinsics(builder);

        Debug.Log("====== Networking: Built Intrinsics");

        float[] position = new float[] { inputHandler.PCPose.position.x, inputHandler.PCPose.position.y, inputHandler.PCPose.position.z };
        VectorOffset positionval = ARVis.Flatbuffers.Pose.CreatePositionVector(builder, position);
        float[] rotation = new float[] { inputHandler.PCPose.rotation.x, inputHandler.PCPose.rotation.y, inputHandler.PCPose.rotation.z, inputHandler.PCPose.rotation.w };
        VectorOffset rotationval = ARVis.Flatbuffers.Pose.CreateRotationVector(builder, rotation);
        ARVis.Flatbuffers.Pose.StartPose(builder);
        ARVis.Flatbuffers.Pose.AddPosition(builder, positionval);
        ARVis.Flatbuffers.Pose.AddPosition(builder, rotationval);
        Offset<ARVis.Flatbuffers.Pose> pose = ARVis.Flatbuffers.Pose.EndPose(builder);

        Debug.Log("====== Networking: Built Pose");

        byte[] colorbytes = inputHandler.capturedImage;
        var c = Frame.CreateColorVector(builder, inputHandler.capturedImage);
        Debug.Log("Image data length is " + inputHandler.capturedImage.Length);

        Debug.Log("====== Networking: Built Color");

        byte[] zerobytes = { };
        var cd = Frame.CreateColordistortVector(builder, zerobytes);
        var dd = Frame.CreateDepthdistortVector(builder, zerobytes);

        var depth = Frame.CreateDepthVector(builder, zerobytes);
        if (inputHandler.getDepth)
        {
            depth = Frame.CreateDepthVector(builder, inputHandler.capturedDepth);
        }
        short tsus = 0;

        Frame.StartFrame(builder);
        Frame.AddColor(builder, c);
        Frame.AddIntrinsics(builder, intrinsics);
        Frame.AddColordistort(builder, cd);
        Frame.AddDepthdistort(builder, dd);
        Frame.AddTsus(builder, tsus);
        Frame.AddPose(builder, pose);

        

        Offset<Frame> f = Frame.EndFrame(builder);
        builder.Finish(f.Value);
    }

    public void Start(Action<byte[]> callback)
    {
        running = true;
        receiveThread.Start(callback);
    }

    public void Stop()
    {
        running = false;
        receiveThread.Join();
    }
}


public class Client : MonoBehaviour
{
    private readonly ConcurrentQueue<Action> runOnMainThread = new ConcurrentQueue<Action>();
    private Receiver receiver;
    


    public void Start()
    {

        ForceDotNet.Force();  // If you have multiple sockets in the following threads
        receiver = new Receiver();
        receiver.Start((byte[] d) => runOnMainThread.Enqueue(() =>
        {
            
            // *******
            // You can manage your server's response (stored in the argument d) from here
            // *******

            Debug.Log("Reply Recieved");
        }
        ));
    }

    public void Update()
    {
        if (!runOnMainThread.IsEmpty)
        {
            Action action;
            while (runOnMainThread.TryDequeue(out action))
            {
                action.Invoke();
            }
        }
    }

    private void OnDestroy()
    {
        receiver.Stop();
        NetMQConfig.Cleanup();
    }
}