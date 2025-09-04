# pip install pyzmq opencv-python

DEFAULT_RES_X = 1200
DEFAULT_RES_Y = 900

try:
    import time
    import zmq
    #import cv2
    import flatbuffers
    import ARVis.Flatbuffers.Frame 
    import numpy as np
    from PIL import Image
except Exception as e:
    print(f"Unexpected Error has occured: {e}")
    input("Press Enter to continue...")


try:
    context = zmq.Context()
    socket = context.socket(zmq.REP)
    # socket.bind('tcp://*:5555')
    socket.bind('tcp://*:5555')

    #capture = cv2.VideoCapture(0)
except Exception as e:
    print(f"Unexpected Error has occured: {e}")
    input("Press Enter to continue...")


while True:
    try:
        # start = time.time()
        msg = socket.recv()

        if (len(msg) > 0):
            print("REC FRAME")
            msg_frame = ARVis.Flatbuffers.Frame.Frame.GetRootAs(msg, 0)

            intrin_buf = msg_frame.Intrinsics()
            
            pose_buf = msg_frame.Pose()

            color_buf = msg_frame.ColorAsNumpy()
            if (len(color_buf) > 0):
                print("=== Color Buffer Found")
                try:
                    display_x = DEFAULT_RES_X
                    display_y = DEFAULT_RES_Y
                    if (intrin_buf.Resolution().X() > 0 and intrin_buf.Resolution().Y() > 0):
                        display_x = intrin_buf.Resolution().X()
                        display_y = intrin_buf.Resolution().Y()
            
                    img = Image.frombytes("RGBA", (display_x, display_y), color_buf)
                    img.show()
                except Exception as e:
                    print(f"Image Display Error: {e}")
            else:
                print("WARNING: Color buffer length is 0")

            socket.send(b"Recieved")
        else:
            socket.send(b"")
            print("Recieved Empty Frame")
            
        end = time.time()
        #print('FPS:', 1 / (end - start))
    except Exception as e:
        print(f"Unexpected Error has occured: {e}")
        input("Press Enter to continue...")

