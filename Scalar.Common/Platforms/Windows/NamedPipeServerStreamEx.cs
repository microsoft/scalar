using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Scalar.Platform.Windows
{
    /// <remarks>
    /// The overload of NamedPipeServerStream.ctor with the PipeSecurity parameter was removed
    /// in .NET Standard and .NET Core.
    ///
    /// Unfortunately, the default constructor does not provide WRITE_DAC, so attempting
    /// to use SetAccessControl after construction will always fail.
    /// (https://github.com/dotnet/corefx/issues/31190)
    ///
    /// Since the PipeAccessRights parameter was also removed, we cannot pass the
    /// PipeAccessRights.ChangePermissions option either to provide WRITE_DAC to the pipe.
    /// (https://github.com/dotnet/corefx/issues/24040)
    ///
    /// Instead we must manually create the underlying pipe handle with the correct security
    /// attributes up-front, and then pass this native handle to the NamedPipeServerStream
    /// managed object. Depressingly, the .NET Core codebase already contains all the methods
    /// we need to call to do this, but they are all internal/private. We include a copy of
    /// the minimum required code here to reinstate the removed constructor and functionality.
    ///
    /// All the code below was taken from the .NET Core codebase with comments pointing to
    /// the source file and version.
    /// </remarks>
    internal static class NamedPipeServerStreamEx
    {
        // https://github.com/dotnet/corefx/blob/e753ecfe12d6af9b7fec5dee154395e7d29caed9/src/System.IO.Pipes/src/System/IO/Pipes/NamedPipeServerStream.cs#L20
        private const int MaxAllowedServerInstances = -1;

        // https://github.com/dotnet/corefx/blob/d3911035f2ba3eb5c44310342cc1d654e42aa316/src/Common/src/Interop/Windows/Kernel32/Interop.FileOperations.cs#L21
        private const uint FILE_FLAG_FIRST_PIPE_INSTANCE = 0x00080000;

        // https://github.com/dotnet/corefx/blob/d3911035f2ba3eb5c44310342cc1d654e42aa316/src/Common/src/CoreLib/Interop/Windows/Interop.BOOL.cs
        private enum BOOL : int
        {
            FALSE = 0,
            TRUE = 1,
        }

        // https://github.com/dotnet/corefx/blob/d3911035f2ba3eb5c44310342cc1d654e42aa316/src/Common/src/CoreLib/Interop/Windows/Kernel32/Interop.SECURITY_ATTRIBUTES.cs
        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            internal uint nLength;
            internal IntPtr lpSecurityDescriptor;
            internal BOOL bInheritHandle;
        }

        // https://github.com/dotnet/corefx/blob/8c5260061b11323dfd97fbab614d54402405513f/src/Common/src/Interop/Windows/Kernel32/Interop.CreateNamedPipe.cs
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true, BestFitMapping = false, EntryPoint = "CreateNamedPipeW")]
        private static extern SafePipeHandle CreateNamedPipe(
            string pipeName,
            int openMode,
            int pipeMode,
            int maxInstances,
            int outBufferSize,
            int inBufferSize,
            int defaultTimeout,
            ref SECURITY_ATTRIBUTES securityAttributes);

        // https://github.com/dotnet/corefx/blob/8c5260061b11323dfd97fbab614d54402405513f/src/System.IO.Pipes/src/System/IO/Pipes/PipeStream.Windows.cs#L415-L435
        private static unsafe SECURITY_ATTRIBUTES GetSecAttrs(HandleInheritability inheritability, PipeSecurity pipeSecurity, ref GCHandle pinningHandle)
        {
            SECURITY_ATTRIBUTES secAttrs = default;
            secAttrs.nLength = (uint)sizeof(SECURITY_ATTRIBUTES);

            if ((inheritability & HandleInheritability.Inheritable) != 0)
            {
                secAttrs.bInheritHandle = BOOL.TRUE;
            }

            if (pipeSecurity != null)
            {
                byte[] securityDescriptor = pipeSecurity.GetSecurityDescriptorBinaryForm();
                pinningHandle = GCHandle.Alloc(securityDescriptor, GCHandleType.Pinned);
                fixed (byte* pSecurityDescriptor = securityDescriptor)
                {
                    secAttrs.lpSecurityDescriptor = (IntPtr)pSecurityDescriptor;
                }
            }

            return secAttrs;
        }

        // https://github.com/dotnet/corefx/blob/e753ecfe12d6af9b7fec5dee154395e7d29caed9/src/System.IO.Pipes/src/System/IO/Pipes/NamedPipeServerStream.Windows.cs#L31-L108
        private static SafePipeHandle CreatePipeHandle(string pipeName, PipeDirection direction, int maxNumberOfServerInstances,
            PipeTransmissionMode transmissionMode, PipeOptions options, int inBufferSize, int outBufferSize,
            PipeSecurity pipeSecurity, HandleInheritability inheritability, PipeAccessRights additionalAccessRights)
        {
            Debug.Assert(pipeName != null && pipeName.Length != 0, "fullPipeName is null or empty");
            Debug.Assert(direction >= PipeDirection.In && direction <= PipeDirection.InOut, "invalid pipe direction");
            Debug.Assert(inBufferSize >= 0, "inBufferSize is negative");
            Debug.Assert(outBufferSize >= 0, "outBufferSize is negative");
            Debug.Assert((maxNumberOfServerInstances >= 1 && maxNumberOfServerInstances <= 254) || (maxNumberOfServerInstances == MaxAllowedServerInstances), "maxNumberOfServerInstances is invalid");
            Debug.Assert(transmissionMode >= PipeTransmissionMode.Byte && transmissionMode <= PipeTransmissionMode.Message, "transmissionMode is out of range");

            string fullPipeName = Path.GetFullPath(@"\\.\pipe\" + pipeName);

            // Make sure the pipe name isn't one of our reserved names for anonymous pipes.
            if (string.Equals(fullPipeName, @"\\.\pipe\anonymous", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentOutOfRangeException(nameof(pipeName));
            }

            if ((options & PipeOptions.CurrentUserOnly) != 0)
            {
                Debug.Assert(pipeSecurity == null);

                using (WindowsIdentity currentIdentity = WindowsIdentity.GetCurrent())
                {
                    SecurityIdentifier identifier = currentIdentity.Owner;

                    // Grant full control to the owner so multiple servers can be opened.
                    // Full control is the default per MSDN docs for CreateNamedPipe.
                    PipeAccessRule rule = new PipeAccessRule(identifier, PipeAccessRights.FullControl, AccessControlType.Allow);
                    pipeSecurity = new PipeSecurity();

                    pipeSecurity.AddAccessRule(rule);
                    pipeSecurity.SetOwner(identifier);
                }

                // PipeOptions.CurrentUserOnly is special since it doesn't match directly to a corresponding Win32 valid flag.
                // Remove it, while keeping others untouched since historically this has been used as a way to pass flags to CreateNamedPipe
                // that were not defined in the enumeration.
                options &= ~PipeOptions.CurrentUserOnly;
            }

            int openMode = (int) direction |
                           (int) (maxNumberOfServerInstances == 1 ? FILE_FLAG_FIRST_PIPE_INSTANCE : 0) |
                           (int) options |
                           (int) additionalAccessRights;

            // We automatically set the ReadMode to match the TransmissionMode.
            int pipeModes = (int)transmissionMode << 2 | (int)transmissionMode << 1;

            // Convert -1 to 255 to match win32 (we asserted that it is between -1 and 254).
            if (maxNumberOfServerInstances == MaxAllowedServerInstances)
            {
                maxNumberOfServerInstances = 255;
            }

            var pinningHandle = new GCHandle();
            try
            {
                SECURITY_ATTRIBUTES secAttrs = GetSecAttrs(inheritability, pipeSecurity, ref pinningHandle);
                SafePipeHandle handle = CreateNamedPipe(fullPipeName, openMode, pipeModes,
                    maxNumberOfServerInstances, outBufferSize, inBufferSize, 0, ref secAttrs);

                if (handle.IsInvalid)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                return handle;
            }
            finally
            {
                if (pinningHandle.IsAllocated)
                {
                    pinningHandle.Free();
                }
            }
        }

        /// <summary>
        /// Create a named pipe server stream with pipe security options.
        /// </summary>
        public static NamedPipeServerStream Create(string pipeName, PipeDirection direction, int maxNumberOfServerInstances,
            PipeTransmissionMode transmissionMode, PipeOptions options, int inBufferSize, int outBufferSize,
            PipeSecurity pipeSecurity, HandleInheritability inheritability)
        {
            SafePipeHandle handle = CreatePipeHandle(pipeName, direction, maxNumberOfServerInstances,
                transmissionMode, options, inBufferSize, outBufferSize,
                pipeSecurity, inheritability, (PipeAccessRights)0);

            bool isAsync = (options & PipeOptions.Asynchronous) != 0;
            bool isConnected = false;

            return new NamedPipeServerStream(direction, isAsync, isConnected, handle);
        }
    }
}
