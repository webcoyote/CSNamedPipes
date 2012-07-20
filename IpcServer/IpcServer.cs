using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.ServiceProcess;
using Coho.IpcLibrary;

using System.ComponentModel;
using System.Data;
using System.Diagnostics;


public class DemoApp {
    public static void Main (string [] args) {

        // To configure this application to run as a service, run these commands as an admin:
        // sc create SERVICE_NAME type= own start= auto binPath= "FULL_PATH_TO_EXE\IpcServer.exe /service"
        // sc failure SERVICE_NAME reset= 86400 actions= restart/60000/restart/60000/restart/60000
        // sc start SERVICE_NAME
        //
        // Other useful commands
        // sc stop SERVICE_NAME
        // sc delete SERVICE_NAME


        if (args.Length > 0 && args[0] == "/service") {
            // Run as a service application
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[] { new DemoService() };
            ServiceBase.Run(ServicesToRun);
        }
        else {
            // Run as a Windows console application
            DemoIpcServer m_server = new DemoIpcServer();
            m_server.Start();

            // Since all the requests are issued asynchronously, the constructors are likely to return
            // before all the requests are complete. The call below stops the application from terminating
            // until we see all the responses displayed.
            Thread.Sleep(1000);
            Console.WriteLine("\nPress return to shutdown server");
            Console.ReadLine();

            m_server.Stop();

            Console.WriteLine("\nComplete! Press return to exit program");
            Console.ReadLine();
        }
    }
}

public partial class DemoService : ServiceBase {
    DemoIpcServer m_server;

    public DemoService() {
        m_server = new DemoIpcServer();
    }
 
    protected override void OnStart(string[] args) {
        m_server.Start();
    }
 
    protected override void OnStop() {
        m_server.Stop();
    }
}


public class DemoIpcServer : IpcCallback {
    private IpcServer m_srv;
    private Int32 m_count;

    public void Start() {
        // Create ten instances of listening pipes so that if there are many clients
        // trying to connect in a short interval (with short timeouts) the clients are
        // less likely to fail to connect.
        m_srv = new IpcServer("ExamplePipeName", this, 10);
    }

    public void Stop() {
        m_srv.IpcServerStop();
    }

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
