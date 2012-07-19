using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Security.AccessControl;
using System.Security.Principal;
using Coho.IpcLibrary;


public class DemoApp {
    public static void Main () {
        // Create the callback interface class
        DemoIpcServer server = new DemoIpcServer();

        // Create ten instances of listeing pipes so that if there are many clients
        // trying to connect in a short interval (with short timeouts) the clients are
        // less likely to fail to connect.
        IpcServer srv = new IpcServer("ExamplePipeName", server, 10);

        // Since all the requests are issued asynchronously, the constructors are likely to return
        // before all the requests are complete. The call below stops the application from terminating
        // until we see all the responses displayed.
        Thread.Sleep(1000);
        Console.WriteLine("\nPress return to shutdown server");
        Console.ReadLine();

        srv.IpcServerStop();

        Console.WriteLine("\nComplete! Press return to exit program");
        Console.ReadLine();
    }
}


public class DemoIpcServer : IpcCallback {
    // Sample code
    private Int32 m_count;

    public void OnAsyncConnect (PipeStream pipe, out Object state) {
        // Sample code
        Int32 count = Interlocked.Increment(ref m_count);
        Console.WriteLine("Connected: " + count);
        state = count;
    }

    public void OnAsyncDisconnect (PipeStream pipe, Object state) {
        // Sample code
        Console.WriteLine("Disconnected: " + (Int32) state);
    }

    public void OnAsyncMessage (PipeStream pipe, Byte [] data, Int32 bytes, Object state) {
        // Sample code
        Console.WriteLine("Message: " + (Int32) state + " bytes: " + bytes);
        data = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(data, 0, bytes).ToUpper().ToCharArray());

        // Write results
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
