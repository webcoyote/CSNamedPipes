using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Diagnostics;
using Coho.IpcLibrary;


public class DemoApp {
    public static void Main () {
        // For testing create multiple clients (synchronously for simplicity, but async works too)
        for (Int32 n = 0; n < 10; n++) {
            Thread t = new Thread(DemoApp.ThreadProc);
            t.Start(n);
        }

        // The call below stops the application from terminating
        // until we see all the responses displayed.
        Console.ReadLine();
    }
                
    public static void ThreadProc(Object n) {
        Int32 index = (Int32) n;
        IpcClientPipe cli = new IpcClientPipe(".", "ExamplePipeName");

        PipeStream pipe;
        try {
            pipe = cli.Connect(10);
        }
        catch (Exception e) {
            Console.WriteLine("Connection failed: " + e);
            return;
        }

        // Asynchronously send data to the server
        string message = "Test request " + index;
        Byte[] output = Encoding.UTF8.GetBytes(message);
        Debug.Assert(output.Length < IpcServer.SERVER_IN_BUFFER_SIZE);
        pipe.Write(output, 0, output.Length);

        // Read the result
        Byte[] data = new Byte[IpcServer.SERVER_OUT_BUFFER_SIZE];
        Int32 bytesRead = pipe.Read(data, 0, data.Length);
        Console.WriteLine("Server response: " + Encoding.UTF8.GetString(data, 0, bytesRead));
        
        // Done with this one
        pipe.Close();
    }
        
}
