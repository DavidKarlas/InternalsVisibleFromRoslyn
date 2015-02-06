using System;
using System.IO;
using Mono.Cecil;
using System.Runtime.CompilerServices;
using System.Threading;

namespace InternalsVisibleFromRoslyn
{
	public class MainClass
	{
		readonly static string[] DllsNamesToInject = {
			"ICSharpCode.NRefactory6.CSharp, PublicKey=00240000048000009400000006020000002400005253413100040000010001004dcf3979c4e902efa4dd2163a039701ed5822e6f1134d77737296abbb97bf0803083cfb2117b4f5446a217782f5c7c634f9fe1fc60b4c11d62c5b3d33545036706296d31903ddcf750875db38a8ac379512f51620bb948c94d0831125fbc5fe63707cbb93f48c1459c4d1749eb7ac5e681a2f0d6d7c60fa527a3c0b8f92b02bf",
			"ICSharpCode.NRefactory6.CSharp.Refactoring, PublicKey=00240000048000009400000006020000002400005253413100040000010001004dcf3979c4e902efa4dd2163a039701ed5822e6f1134d77737296abbb97bf0803083cfb2117b4f5446a217782f5c7c634f9fe1fc60b4c11d62c5b3d33545036706296d31903ddcf750875db38a8ac379512f51620bb948c94d0831125fbc5fe63707cbb93f48c1459c4d1749eb7ac5e681a2f0d6d7c60fa527a3c0b8f92b02bf",
			"OmniSharp",
			"Scrawl.CodeEngine.Roslyn"
		};

		//TODO: Add VisualBasic
		readonly static string[] Packages = {
			"Microsoft.CodeAnalysis.Analyzers",
			"Microsoft.CodeAnalysis.Common",
			"Microsoft.CodeAnalysis.CSharp",
			"Microsoft.CodeAnalysis.Workspaces.Common",
			"Microsoft.CodeAnalysis.CSharp.Workspaces",
			"Microsoft.CodeAnalysis.EditorFeatures.Text",
			"Microsoft.CodeAnalysis.Features.Common",
			"Microsoft.CodeAnalysis.CSharp.Features",
		};

		readonly static string PackagesOutputFolder = "PackagesOutput";
		readonly static string TemplatesFolder = "Templates";
		readonly static string WorkFolder = "Work";

		readonly static string Pdb2MdbPath = @"C:\Program Files (x86)\Mono\bin\pdb2mdb.bat";
		readonly static string NuGetExePath = "NuGet.exe";


		public static void Main (string[] args)
		{
			//TODO: Create real help
			if (args.Length != 4) {
				Console.WriteLine ("Usage: InternalsVisibleFromRoslyn.exe path-to-roslyn-dlls version authors ownders");
				Console.WriteLine ("e.g: InternalsVisibleFromRoslyn.exe ..\\..\\..\\roslyn\\Binaries\\Debug 1.0.0-UNOFFICAL-20150206-1 ČungaLunga ČungaLunga");
				return;
			}
			//TODO: Add more options, mostly to .pdb, and later maybe direct push nuget settings...
			string RoslynBinaries = args [0];
			string Version = args [1];
			string Authors = args [2];
			string Owners = args [3];

			if (!Directory.Exists (TemplatesFolder)) {
				Console.WriteLine ("Missing templates folder:" + TemplatesFolder);
				return;
			}
			if (!Directory.Exists (RoslynBinaries)) {
				Console.WriteLine ("Missing RoslynBin folder:" + RoslynBinaries);
				return;
			}
			if (!Directory.Exists (TemplatesFolder)) {
				Console.WriteLine ("Missing NuGet:" + NuGetExePath);
				return;
			}

			bool createMdb = File.Exists (Pdb2MdbPath);
			var resolver = new DefaultAssemblyResolver ();
			resolver.AddSearchDirectory (RoslynBinaries);
			var parameters = new ReaderParameters {
				AssemblyResolver = resolver,
			};
			if (Directory.Exists (WorkFolder)) {
				Directory.Delete (WorkFolder, true);
			}
			DirectoryCopy (TemplatesFolder, WorkFolder, true);

			if (Directory.Exists (PackagesOutputFolder)) {
				Directory.Delete (PackagesOutputFolder, true);
			}
			Thread.Sleep (100);
			Directory.CreateDirectory (PackagesOutputFolder);
			Thread.Sleep (100);//Don't ask me why this thread sleeps are here, tnx
			foreach (var package in Packages) {
				var packageAssemblyName = package;
				if (package == "Microsoft.CodeAnalysis.Analyzers") {
					packageAssemblyName = null;//I don't care about this anaylizers but don't want to change dependencies...
				} else if (package.EndsWith (".Common")) {
					packageAssemblyName = packageAssemblyName.Remove (packageAssemblyName.Length - ".Common".Length);
				}
				var workFolder = Path.Combine (WorkFolder, package);

				var nuspecFile = Path.Combine (workFolder, package + ".nuspec");
				var nuspecContent = File.ReadAllText (nuspecFile);
				nuspecContent = nuspecContent.Replace ("%_version_%", Version);
				nuspecContent = nuspecContent.Replace ("%_authors_%", Authors);
				nuspecContent = nuspecContent.Replace ("%_owners_%", Owners);
				File.WriteAllText (nuspecFile, nuspecContent);
						
				var outputNuPkg = Path.Combine (PackagesOutputFolder, package + ".nupkg");
				if (packageAssemblyName != null) {

					var extensionsToCopy = new [] {//Remove .pdb if you don't like it
						".dll",
						".pdb",
						".xml"
					};
					Directory.CreateDirectory (Path.Combine (workFolder, "lib", "net45"));
					Directory.CreateDirectory (Path.Combine (workFolder, "lib", "portable-net45%2Bwin8"));
					foreach (var extension in extensionsToCopy) {
						var fileName = packageAssemblyName + extension;
						File.Copy (Path.Combine (RoslynBinaries, fileName), Path.Combine (workFolder, "lib", "net45", fileName));
						File.Copy (Path.Combine (RoslynBinaries, fileName), Path.Combine (workFolder, "lib", "portable-net45%2Bwin8", fileName));
						fileName = packageAssemblyName + ".Desktop" + extension;
						if (File.Exists (Path.Combine (RoslynBinaries, fileName))) {
							File.Copy (Path.Combine (RoslynBinaries, fileName), Path.Combine (workFolder, "lib", "net45", fileName));
						}
					}

					//Doing CecilMagic
					foreach (var dllPath in Directory.GetFiles(workFolder,"*.dll",SearchOption.AllDirectories)) {
						var asm = ModuleDefinition.ReadModule (dllPath, parameters);
						foreach (var dllNameToInject in DllsNamesToInject) {
							CustomAttribute ca = new CustomAttribute (asm.Import (typeof(InternalsVisibleToAttribute).GetConstructor (new []{ typeof(string) })));
							ca.ConstructorArguments.Add (new CustomAttributeArgument (asm.TypeSystem.String, dllNameToInject));
							asm.Assembly.CustomAttributes.Add (ca);
						}
						asm.Write (dllPath);
						if (createMdb) {
							//TODO: Check output of pdb2mdb.bat
							var mdbPsi = new System.Diagnostics.ProcessStartInfo (Pdb2MdbPath, dllPath);
							mdbPsi.UseShellExecute = false;
							mdbPsi.CreateNoWindow = true;
							System.Diagnostics.Process.Start (mdbPsi).WaitForExit ();
						}
					}
				}

				//TODO: Check output of nuget.exe
				var psi = new System.Diagnostics.ProcessStartInfo (NuGetExePath, "pack " + Path.GetFileName (nuspecFile));
				psi.WorkingDirectory = Path.GetFullPath (Path.GetDirectoryName (nuspecFile));
				psi.UseShellExecute = false;
				psi.CreateNoWindow = true;
				System.Diagnostics.Process.Start (psi).WaitForExit ();
				File.Copy (Path.Combine (workFolder, package + "." + Version + ".nupkg"), Path.Combine (PackagesOutputFolder, package + "." + Version + ".nupkg"));
				//TODO: Automatic push?
			}
			Console.WriteLine ("WELL DONE! Now go to " + PackagesOutputFolder + " folder and publish this NuGets!");
		}

		static void DirectoryCopy (string sourceDirName, string destDirName, bool copySubDirs)
		{
			// Get the subdirectories for the specified directory.
			DirectoryInfo dir = new DirectoryInfo (sourceDirName);
			DirectoryInfo[] dirs = dir.GetDirectories ();

			if (!dir.Exists) {
				throw new DirectoryNotFoundException (
					"Source directory does not exist or could not be found: "
					+ sourceDirName);
			}

			// If the destination directory doesn't exist, create it. 
			if (!Directory.Exists (destDirName)) {
				Directory.CreateDirectory (destDirName);
			}

			// Get the files in the directory and copy them to the new location.
			FileInfo[] files = dir.GetFiles ();
			foreach (FileInfo file in files) {
				string temppath = Path.Combine (destDirName, file.Name);
				file.CopyTo (temppath, false);
			}

			// If copying subdirectories, copy them and their contents to new location. 
			if (copySubDirs) {
				foreach (DirectoryInfo subdir in dirs) {
					string temppath = Path.Combine (destDirName, subdir.Name);
					DirectoryCopy (subdir.FullName, temppath, copySubDirs);
				}
			}
		}
	}
}
