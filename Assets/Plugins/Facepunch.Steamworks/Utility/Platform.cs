using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace Steamworks
{
	internal static class Platform
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_WIN64 || PLATFORM_WIN32
		public const int StructPlatformPackSize = 8;
		public const string LibraryName = "steam_api64";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX || PLATFORM_POSIX
		public const int StructPlatformPackSize = 4;
		public const string LibraryName = "libsteam_api";
#else
		public const int StructPlatformPackSize = 4;
		public const string LibraryName = "steam_api";
#endif

		public const CallingConvention CC = CallingConvention.Cdecl;
		public const int StructPackSize = 4;
	}
}
