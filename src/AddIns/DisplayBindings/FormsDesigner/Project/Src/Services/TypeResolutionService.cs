// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Daniel Grunwald" email="daniel@danielgrunwald.de"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.ComponentModel;
using System.ComponentModel.Design;
using ICSharpCode.SharpDevelop.Project;
using ICSharpCode.SharpDevelop.Dom;
using ICSharpCode.Core;
using System.Diagnostics ;


using HashFunction = System.Security.Cryptography.SHA1Managed;

namespace ICSharpCode.FormsDesigner.Services
{
	public class TypeResolutionService : ITypeResolutionService
	{
		readonly static List<Assembly> designerAssemblies = new List<Assembly>();
		/// <summary>
		/// string "filename:size" -> Assembly
		/// </summary>
		readonly static Dictionary<string, Assembly> assemblyDict = new Dictionary<string, Assembly>();
		
		/// <summary>
		/// List of assemblies used by the form designer. This static list is not an optimal solution,
		/// but better than using AppDomain.CurrentDomain.GetAssemblies(). See SD2-630.
		/// </summary>
		public static List<Assembly> DesignerAssemblies {
			get {
				return designerAssemblies;
			}
		}
		
		static TypeResolutionService()
		{
			ClearMixedAssembliesTemporaryFiles();
			DesignerAssemblies.Add(ProjectContentRegistry.MscorlibAssembly);
			DesignerAssemblies.Add(ProjectContentRegistry.SystemAssembly);
			DesignerAssemblies.Add(typeof(System.Drawing.Point).Assembly);
		}
		
		[System.Runtime.InteropServices.DllImport("kernel32.dll")]
		private static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, int dwFlags);
		const int MOVEFILE_DELAY_UNTIL_REBOOT = 0x00000004;
		
		static void MarkFileToDeleteOnReboot(string fileName)
		{
			MoveFileEx(fileName, null, MOVEFILE_DELAY_UNTIL_REBOOT);
		}

		static void ClearMixedAssembliesTemporaryFiles()
		{
			string[] files = Directory.GetFiles(Path.GetTempPath(), "*.sd_forms_designer_mixed_assembly.dll");
			foreach (string fileName in files) {
				try {
					File.Delete(fileName);
				} catch {}
			}
			/* We don't need to debug controls inside the forms designer
			files = Directory.GetFiles(Path.GetTempPath(), "*.pdb");
			foreach (string fileName in files) {
				try {
					File.Delete(fileName);
				} catch {}
			}*/
		}

		
		string formSourceFileName;
		IProjectContent callingProject;
		
		/// <summary>
		/// Gets the project content of the project that created this TypeResolutionService.
		/// Returns null when no calling project was specified.
		/// </summary>
		public IProjectContent CallingProject {
			get {
				if (formSourceFileName != null) {
					if (ProjectService.OpenSolution != null) {
						IProject p = ProjectService.OpenSolution.FindProjectContainingFile(formSourceFileName);
						if (p != null) {
							callingProject = ParserService.GetProjectContent(p);
						}
					}
					formSourceFileName = null;
				}
				return callingProject;
			}
		}
		
		public TypeResolutionService()
		{
		}
		
		public TypeResolutionService(string formSourceFileName)
		{
			this.formSourceFileName = formSourceFileName;
		}
		
		/// <summary>
		/// Loads the assembly represented by the project content. Returns null on failure.
		/// </summary>
		public static Assembly LoadAssembly(IProjectContent pc)
		{
			// load dependencies of current assembly
			foreach (IProjectContent rpc in pc.ReferencedContents) {
				if (rpc is ParseProjectContent) {
					LoadAssembly(rpc);
				} else if (rpc is ReflectionProjectContent) {
					if (!(rpc as ReflectionProjectContent).IsGacAssembly)
						LoadAssembly(rpc);
				}
			}
			
			if (pc.Project != null) {
				return LoadAssembly(pc.Project.OutputAssemblyFullPath);
			} else if (pc is ReflectionProjectContent) {
				return LoadAssembly((pc as ReflectionProjectContent).AssemblyLocation);
			} else {
				return null;
			}
		}
		
		/// <summary>
		/// Loads the file in none-locking mode. Returns null on failure.
		/// </summary>
		public static Assembly LoadAssembly(string fileName)
		{
			if (!File.Exists(fileName))
				return null;
			byte[] data = File.ReadAllBytes(fileName);
			
			string hash = fileName + ":" + File.GetLastWriteTimeUtc(fileName);
			using (HashFunction hashFunction = new HashFunction()) {
				hash = Convert.ToBase64String(hashFunction.ComputeHash(data));
			}
			lock (assemblyDict) {
				Assembly asm;
				if (assemblyDict.TryGetValue(hash, out asm))
					return asm;
				try {
					asm = Assembly.Load(data);
				} catch (BadImageFormatException e) {
					if (e.Message.Contains("HRESULT: 0x8013141D")) {
						//netmodule
						string tempPath = Path.GetTempFileName();
						File.Delete(tempPath);
						tempPath += ".sd_forms_designer_netmodule_assembly.dll";
						
						try {
							//convert netmodule to assembly
							Process p = new Process();
							p.StartInfo.UseShellExecute = false;
							p.StartInfo.FileName = Path.GetDirectoryName(typeof(object).Module.FullyQualifiedName) + Path.DirectorySeparatorChar + "al.exe";
							p.StartInfo.Arguments = "\"" + fileName +"\" /out:\"" + tempPath + "\"";
							p.StartInfo.CreateNoWindow = true;
							p.Start();
							p.WaitForExit();
							
							if(p.ExitCode == 0 && File.Exists(tempPath)) {
								byte[] asm_data = File.ReadAllBytes(tempPath);
								asm = Assembly.Load(asm_data);
								asm.LoadModule(Path.GetFileName(fileName), data);
							}
						} catch (Exception ex) {
							MessageService.ShowError(ex, "Error calling linker for netmodule");
						}
						try {
							File.Delete(tempPath);
						} catch {}
					} else {
						throw; // don't ignore other load errors
					}
				} catch (FileLoadException e) {
					if (e.Message.Contains("HRESULT: 0x80131402")) {
						//this is C++/CLI Mixed assembly which can only be loaded from disk, not in-memory
						string tempPath = Path.GetTempFileName();
						File.Delete(tempPath);
						tempPath += ".sd_forms_designer_mixed_assembly.dll";
						File.Copy(fileName, tempPath);
						
						/* We don't need to debug controls inside the forms designer
						string pdbpath = Path.GetDirectoryName(fileName) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(fileName) + ".pdb";
						if (File.Exists(pdbpath)) {
							string newpdbpath = Path.GetTempPath() + Path.DirectorySeparatorChar + Path.GetFileName(pdbpath);
							try {
								File.Copy(pdbpath, newpdbpath);
								MarkFileToDeleteOnReboot(newpdbpath);
							} catch {
							}
						}
						 */
						asm = Assembly.LoadFile(tempPath);
						MarkFileToDeleteOnReboot(tempPath);
					} else {
						throw; // don't ignore other load errors
					}
				}

				lock (designerAssemblies) {
					if (!designerAssemblies.Contains(asm))
						designerAssemblies.Add(asm);
				}
				assemblyDict[hash] = asm;
				return asm;
			}
		}
		
		public Assembly GetAssembly(AssemblyName name)
		{
			return GetAssembly(name, false);
		}
		
		public Assembly GetAssembly(AssemblyName name, bool throwOnError)
		{
			try {
				Assembly asm = Assembly.Load(name);
				lock (designerAssemblies) {
					if (!designerAssemblies.Contains(asm))
						designerAssemblies.Add(asm);
				}
				return asm;
			} catch (System.IO.FileLoadException) {
				if (throwOnError)
					throw;
				return null;
			}
		}
		
		public string GetPathOfAssembly(AssemblyName name)
		{
			Assembly assembly = GetAssembly(name);
			if (assembly != null) {
				return assembly.Location;
			}
			return null;
		}
		
		public Type GetType(string name)
		{
			return GetType(name, false, false);
		}
		
		public Type GetType(string name, bool throwOnError)
		{
			return GetType(name, throwOnError, false);
		}
		
		public Type GetType(string name, bool throwOnError, bool ignoreCase)
		{
			if (name == null || name.Length == 0) {
				return null;
			}
			if (IgnoreType(name)) {
				return null;
			}
			try {
				
				Type type = null;
				int idx = name.IndexOf(",");
				
				if (idx < 0) {
					IProjectContent pc = this.CallingProject;
					if (pc != null) {
						IClass foundClass = pc.GetClass(name);
						if (foundClass != null) {
							Assembly assembly = LoadAssembly(foundClass.ProjectContent);
							if (assembly != null) {
								type = assembly.GetType(name, false, ignoreCase);
							}
						}
					}
				}
				
				// type lookup for typename, assembly, xyz style lookups
				if (type == null && idx > 0) {
					string[] splitName = name.Split(',');
					string typeName     = splitName[0];
					string assemblyName = splitName[1].Substring(1);
					Assembly assembly = null;
					try {
						assembly = Assembly.Load(assemblyName);
					} catch (Exception e) {
						LoggingService.Error(e);
					}
					if (assembly != null) {
						lock (designerAssemblies) {
							if (!designerAssemblies.Contains(assembly))
								designerAssemblies.Add(assembly);
						}
						type = assembly.GetType(typeName, false, ignoreCase);
					} else {
						type = Type.GetType(typeName, false, ignoreCase);
					}
				}

				if (type == null) {
					lock (designerAssemblies) {
						foreach (Assembly asm in DesignerAssemblies) {
							Type t = asm.GetType(name, false);
							if (t != null) {
								return t;
							}
						}
					}
				}
				
				if (throwOnError && type == null)
					throw new TypeLoadException(name + " not found by TypeResolutionService");
				
				return type;
			} catch (Exception e) {
				LoggingService.Error(e);
			}
			return null;
		}
		
		public void ReferenceAssembly(AssemblyName name)
		{
			ICSharpCode.Core.LoggingService.Warn("TODO: Add Assembly reference : " + name);
		}
		
		/// <summary>
		/// HACK - Ignore any requests for types from the Microsoft.VSDesigner
		/// assembly.  There are smart tag problems if data adapter
		/// designers are used from this assembly.
		/// </summary>
		bool IgnoreType(string name)
		{
			int idx = name.IndexOf(",");
			if (idx > 0) {
				string[] splitName = name.Split(',');
				string assemblyName = splitName[1].Substring(1);
				if (assemblyName == "Microsoft.VSDesigner") {
					return true;
				}
			}
			return false;
		}
	}
}
