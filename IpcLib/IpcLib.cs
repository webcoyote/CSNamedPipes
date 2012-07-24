using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Collections.Generic;
using System.Diagnostics;

// Ipc => Inter-process communications library

namespace Coho.IpcLibrary {

    // Interface for user code to receive notifications regarding pipe messages
    public interface IpcCallback {
        void OnAsyncConnect (PipeStream pipe, out Object state);
        void OnAsyncDisconnect (PipeStream pipe, Object state);
        void OnAsyncMessage (PipeStream pipe, Byte [] data, Int32 bytes, Object state);
    }

    // Internal data associated with pipes
    struct IpcPipeData {
        public PipeStream  pipe;
        public Object      state;
        public Byte []     data;
    };

    public class IpcServer {
        // TODO: parameterize so they can be passed by application
        public const Int32 SERVER_IN_BUFFER_SIZE = 4096;
        public const Int32 SERVER_OUT_BUFFER_SIZE = 4096;

        private readonly String m_pipename;
        private readonly IpcCallback m_callback;
        private readonly PipeSecurity m_ps;

        private bool m_running;
        private Thread m_bgthread;
        private ManualResetEvent m_bgevent;
        private Dictionary<PipeStream, IpcPipeData> m_pipes = new Dictionary<PipeStream, IpcPipeData>();

        public IpcServer (
            String      pipename,
            IpcCallback callback,
            int         instances
        ) {
            Debug.Assert(!m_running);
            m_running = true;

            // Save parameters for next new pipe
            m_pipename = pipename;
            m_callback = callback;

            // Provide full access to the current user so more pipe instances can be created
            m_ps = new PipeSecurity();
            m_ps.AddAccessRule(
                new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow)
            );
            m_ps.AddAccessRule(
                new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow
                )
            );
 
            // Start accepting connections
            for (int i = 0; i < instances; ++i)
                IpcServerPipeCreate();

            // Create a background thread to detect dead connections
            m_bgevent = new ManualResetEvent(false);
            m_bgthread = new Thread(new ThreadStart(BgThreadProc));
            m_bgthread.Start();
        }

        public void BgThreadProc() {
            // This function blows because it should be unncessary, but it is apparently the only way
            // to detect dead pipes. Alternative methods that don't work:
            // 1. Set ReadTimeout on PipeStream: doesn't work on async sockets
            // 2. Use pinging: requires that this library puts an API dependency on the
            //    application to perform pinging, or forces this library to define a
            //    pinging protocol. Either way, the client then needs to implement pinging - yuck!
            // 3. Have C# BeginRead fail when the remote side gets closed? Since the pipe needs to
            //    detect disconnect so IsConnected works properly, it must already know!
            do {
                lock(this) {
                    foreach(KeyValuePair<PipeStream, IpcPipeData> kvp in m_pipes) {
                        if (!kvp.Key.IsConnected)
                            kvp.Key.Close();
                    }
                }
            } while (!m_bgevent.WaitOne(5000));
        }

        public void IpcServerStop () {
            
            // Close all pipes asynchronously
            lock(this) {
                if (m_running) {
                    m_running = false;
                    m_bgevent.Set();
                    foreach(KeyValuePair<PipeStream, IpcPipeData> kvp in m_pipes)
                        kvp.Key.Close();
                }
            }

            m_bgthread.Join();

            // Wait for all pipes to close
            for (;;) {
                int count;
                lock(this) {
                    count = m_pipes.Count;
                }
                if (count == 0)
                    break;
                Thread.Sleep(5);
            }
        }

        private void IpcServerPipeCreate () {
            // Create message-mode pipe to simplify message transition
            // Assume all messages will be smaller than the pipe buffer sizes
            NamedPipeServerStream pipe = new NamedPipeServerStream(
                m_pipename,
                PipeDirection.InOut,
                -1,     // maximum instances
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough,
                SERVER_IN_BUFFER_SIZE,
                SERVER_OUT_BUFFER_SIZE,
                m_ps
            );

            // Asynchronously accept a client connection
            pipe.BeginWaitForConnection(OnClientConnected, pipe);
        }

        private void OnClientConnected(IAsyncResult result) {
            // Complete the client connection
            NamedPipeServerStream pipe = (NamedPipeServerStream) result.AsyncState;
            pipe.EndWaitForConnection(result);

            // Create client pipe structure
            IpcPipeData pd = new IpcPipeData();
            pd.pipe     = pipe;
            pd.state    = null;
            pd.data     = new Byte[SERVER_IN_BUFFER_SIZE];

            // Add connection to connection list
            bool running;
            lock(this) {
                running = m_running;
                if (running)
                    m_pipes.Add(pd.pipe, pd);
            }

            // If server is still running
            if (running) {
                // Prepare for next connection
                IpcServerPipeCreate();

                // Alert server that client connection exists
                m_callback.OnAsyncConnect(pipe, out pd.state);

                // Accept messages
                BeginRead(pd);
            }
            else {
                pipe.Close();
            }
        }

        private void BeginRead (IpcPipeData pd) {
            // Asynchronously read a request from the client
            try {
                pd.pipe.BeginRead(pd.data, 0, pd.data.Length, OnAsyncMessage, pd);
            }
            catch (Exception) {
                m_callback.OnAsyncDisconnect(pd.pipe, pd.state);
                pd.pipe.Close();
                lock(this) {
                    bool removed = m_pipes.Remove(pd.pipe);
                    Debug.Assert(removed);
                }
            }
        }

        private void OnAsyncMessage(IAsyncResult result) {
            // Async read from client completed
            IpcPipeData pd = (IpcPipeData) result.AsyncState;
            Int32 bytesRead = pd.pipe.EndRead(result);
            if (bytesRead != 0)
               m_callback.OnAsyncMessage(pd.pipe, pd.data, bytesRead, pd.state);
            BeginRead(pd);
        }

    }


    public class IpcClientPipe {
        private readonly NamedPipeClientStream m_pipe;

        public IpcClientPipe(String serverName, String pipename) {
            m_pipe = new NamedPipeClientStream(
                serverName,
                pipename,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough
            );
        }

        public PipeStream Connect (Int32 timeout) {
            // NOTE: will throw on failure
            m_pipe.Connect(timeout);

            // Must Connect before setting ReadMode
            m_pipe.ReadMode = PipeTransmissionMode.Message;

            return m_pipe;
        }
    }

}
