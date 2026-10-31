using BugSplatUnity.Runtime.Client;

namespace BugSplatUnity.Editor
{
	/// <summary>
	/// Uploads symbol files with the same symbol-upload tool, credentials, and database, application
	/// and version as the package's own build step. For add-ons and build scripts that produce
	/// symbols the build step doesn't know about, such as the BugSplat Xbox add-on.
	/// </summary>
	public static class BugSplatSymbolUpload
	{
		/// <summary>
		/// Uploads the files under <paramref name="directory"/> that match <paramref name="globPattern"/>,
		/// for example "**/*.pdb". Blocks until the upload finishes. Returns false if it failed; when no
		/// credentials are set for the database it logs a warning, uploads nothing, and returns true.
		/// </summary>
		public static bool Upload(string directory, string globPattern, BugSplatOptions options)
		{
			var exitCode = -1;
			BuildPostprocessors.UploadSymbols(directory, globPattern, options, code => exitCode = code);
			return exitCode == 0;
		}
	}
}
