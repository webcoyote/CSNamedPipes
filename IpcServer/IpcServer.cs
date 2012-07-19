using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Security.AccessControl;
using System.Security.Principal;
using Coho.IpcLibrary;



public class DemoApp {
    public static void Main () {
        // Start listener(s)
        DemoIpcServer server = new DemoIpcServer();
        IpcServer srv = new IpcServer("ExamplePipeName", server);

        // Since all the requests are issued asynchronously, the constructors are likely to return
        // before all the requests are complete. The call below stops the application from terminating
        // until we see all the responses displayed.
        Console.ReadLine();

        srv.IpcServerStop();

        Console.ReadLine();
    }
}


public class DemoIpcServer : IpcCallback {
    private Int32 m_count;

    public void OnAsyncConnect (PipeStream pipe, out Object state) {
        Int32 count = Interlocked.Increment(ref m_count);
        Console.WriteLine("Connected: " + count);
        state = count;
    }

    public void OnAsyncDisconnect (PipeStream pipe, Object state) {
        Console.WriteLine("Disconnected: " + (Int32) state);
    }

    public void OnAsyncMessage (PipeStream pipe, Byte [] data, Int32 bytes, Object state) {
        Console.WriteLine("Message: " + (Int32) state + " bytes: " + bytes);

        // My sample server just changes all the characters to uppercase
        // But, you can replace this code with any compute-bound operation
        data = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(data, 0, bytes).ToUpper().ToCharArray());
        try {
            pipe.BeginWrite(data, 0, bytes, OnAsyncWriteComplete, pipe);
        }
        catch (Exception) {
            pipe.Close();
        }
    }

    private void OnAsyncWriteComplete(IAsyncResult result) {
        PipeStream pipe = (PipeStream) result.AsyncState;
        pipe.EndWrite(result);
    }
}
