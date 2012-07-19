using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Security.AccessControl;
using System.Security.Principal;
using Coho.IpcLibrary;



public class DemoApp {
    public static void Main () {
        // Issue client requests against the server
        for (Int32 n = 0; n < 10; n++)
            new IpcClientPipe("localhost", "ExamplePipeName", "Request #" + n);

        // Since all the requests are issued asynchronously, the constructors are likely to return
        // before all the requests are complete. The call below stops the application from terminating
        // until we see all the responses displayed.
        Console.ReadLine();
    }

}

