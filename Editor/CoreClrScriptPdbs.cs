using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BugSplatUnity.Editor
{
	// A CoreCLR player keeps its assemblies in a Managed folder at the build root; each one's
	// debug directory names the PDB the compiler wrote under Library/Bee.
	internal static class CoreClrScriptPdbs
	{
		const uint CodeViewDebugType = 2;
		const uint RsdsSignature = 0x53445352;

		internal static bool IsCoreClrBuild(string buildDir) =>
			Directory.Exists(Path.Combine(buildDir, "Managed")) && Directory.Exists(Path.Combine(buildDir, "CoreCLR"));

		internal static List<string> Find(string buildDir)
		{
			var pdbs = new List<string>();
			if (!IsCoreClrBuild(buildDir))
				return pdbs;

			var managedDir = Path.Combine(buildDir, "Managed");
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var dll in Directory.GetFiles(managedDir, "*.dll"))
			{
				if (File.Exists(Path.ChangeExtension(dll, ".pdb")))
					continue;

				var pdb = ExistingFullPath(ReadCodeViewPdbPath(dll));
				if (pdb != null && seen.Add(pdb))
					pdbs.Add(pdb);
			}

			return pdbs;
		}

		// A relative path is relative to the project folder, the editor's working directory.
		static string ExistingFullPath(string path)
		{
			if (string.IsNullOrEmpty(path))
				return null;

			try
			{
				var fullPath = Path.GetFullPath(path);
				return File.Exists(fullPath) ? fullPath : null;
			}
			catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException)
			{
				return null;
			}
		}

		internal static string ReadCodeViewPdbPath(string pePath)
		{
			try
			{
				var image = File.ReadAllBytes(pePath);
				if (image.Length < 0x40 || image[0] != 'M' || image[1] != 'Z')
					return null;

				var pe = BitConverter.ToInt32(image, 0x3C);
				if (pe < 0 || pe + 24 > image.Length || BitConverter.ToUInt32(image, pe) != 0x00004550)
					return null;

				var sectionCount = BitConverter.ToUInt16(image, pe + 6);
				var optionalHeaderSize = BitConverter.ToUInt16(image, pe + 20);
				var optionalHeader = pe + 24;
				var magic = BitConverter.ToUInt16(image, optionalHeader);
				var dataDirectories = optionalHeader + (magic == 0x20B ? 112 : 96);
				var debugDirectory = dataDirectories + 6 * 8;
				if (debugDirectory + 8 > image.Length)
					return null;

				var debugRva = BitConverter.ToUInt32(image, debugDirectory);
				var debugSize = BitConverter.ToUInt32(image, debugDirectory + 4);
				var debugOffset = RvaToOffset(image, optionalHeader + optionalHeaderSize, sectionCount, debugRva);
				if (debugOffset < 0)
					return null;

				for (var entry = debugOffset; entry + 28 <= debugOffset + debugSize && entry + 28 <= image.Length; entry += 28)
				{
					if (BitConverter.ToUInt32(image, entry + 12) != CodeViewDebugType)
						continue;

					var dataSize = BitConverter.ToInt32(image, entry + 16);
					var data = BitConverter.ToInt32(image, entry + 24);
					if (dataSize < 24 || data < 0 || data + dataSize > image.Length || BitConverter.ToUInt32(image, data) != RsdsSignature)
						continue;

					var path = data + 24;
					var end = Array.IndexOf(image, (byte)0, path, data + dataSize - path);
					return Encoding.UTF8.GetString(image, path, (end < 0 ? data + dataSize : end) - path);
				}
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
			{
			}

			return null;
		}

		static int RvaToOffset(byte[] image, int sectionTable, int sectionCount, uint rva)
		{
			for (var i = 0; i < sectionCount; i++)
			{
				var section = sectionTable + i * 40;
				if (section + 40 > image.Length)
					break;

				var virtualSize = BitConverter.ToUInt32(image, section + 8);
				var virtualAddress = BitConverter.ToUInt32(image, section + 12);
				var rawSize = BitConverter.ToUInt32(image, section + 16);
				var rawPointer = BitConverter.ToUInt32(image, section + 20);
				if (rva >= virtualAddress && rva < virtualAddress + Math.Max(virtualSize, rawSize))
					return (int)(rva - virtualAddress + rawPointer);
			}

			return -1;
		}
	}
}
