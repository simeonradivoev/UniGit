using System;
using System.IO;
using UnityEngine;

namespace LibGit2Sharp.Utils
{
	internal static class StreamExtensions
	{
		private const int _DefaultCopyBufferSize = 81920;

		public static void CopyTo(this Stream source,Stream destination)
		{
			if (destination == null)
				throw new ArgumentNullException("destination");
			if (!destination.CanRead && !destination.CanWrite)
				throw new ObjectDisposedException(null, "ObjectDisposed_StreamClosed");
			if (!destination.CanRead && !destination.CanWrite)
				throw new ObjectDisposedException("destination", "ObjectDisposed_StreamClosed");
			if (!destination.CanRead)
				throw new NotSupportedException("NotSupported_UnreadableStream");
			if (!destination.CanWrite)
				throw new NotSupportedException("NotSupported_UnwritableStream");
 
			InternalCopyTo(source,destination, _DefaultCopyBufferSize);
		}

		private static void InternalCopyTo(Stream source,Stream destination, int bufferSize)
		{
			Debug.Assert(destination != null);
			Debug.Assert(destination.CanRead);
			Debug.Assert(destination.CanWrite);
			Debug.Assert(bufferSize > 0);
            
			byte[] buffer = new byte[bufferSize];
			int read;
			while ((read = source.Read(buffer, 0, buffer.Length)) != 0)
				destination.Write(buffer, 0, read);
		}
	}
}
