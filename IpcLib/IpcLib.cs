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
        }

        public void IpcServerStop () {
            // Close all pipes asynchronously
            lock(this) {
                if (m_running) {
                    m_running = false;
                    foreach(KeyValuePair<PipeStream, IpcPipeData> kvp in m_pipes)
                        kvp.Key.Close();
                }
            }

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
