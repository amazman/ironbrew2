using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using IronBrew2.Bytecode_Library.Bytecode;
using IronBrew2.Bytecode_Library.IR;
using IronBrew2.Obfuscator;
using IronBrew2.Obfuscator.Control_Flow;
using IronBrew2.Obfuscator.Encryption;
using IronBrew2.Obfuscator.VM_Generation;

namespace IronBrew2
{
	public static class IB2
	{
		public static Random Random = new Random();
		private static Encoding _fuckingLua = Encoding.GetEncoding(28591);

		public static bool Obfuscate(string path, string input, ObfuscationSettings settings, out string error)
		{
			try
			{
				error = "";

				string OS = Environment.OSVersion.Platform == PlatformID.Unix ? "/usr/bin/" : "";
				
				string l = Path.Combine(path, "luac.out");

				if (!File.Exists(input))
					throw new Exception("Invalid input file.");

				Console.WriteLine("Checking file...");
				
				Process proc = new Process
				       {
					       StartInfo =
					       {
						       FileName  = @"C:\Users\amazman\Desktop\ironbrew-2-master\IronBrew2 CLI\bin\Debug\netcoreapp2.0\luac.exe",
						       Arguments = "-o \"" + l + "\" \"" + input + "\"",
						       UseShellExecute = false,
						       RedirectStandardError = true,
						       RedirectStandardOutput = true
					       }
				       };
                Console.WriteLine($"{OS}luac");
				string err = "";
				
				proc.OutputDataReceived += (sender, args) => { err += args.Data; };
				proc.ErrorDataReceived += (sender, args) => { err += args.Data; };

				proc.Start();
				proc.BeginOutputReadLine();
				proc.BeginErrorReadLine();
				proc.WaitForExit();

				error = err;
				
				if (!File.Exists(l))
					return false;
				
				File.Delete(l);
				string t0 = Path.Combine(path, "t0.lua");
				
				Console.WriteLine("Stripping comments...");

				proc = new Process
				       {
					       StartInfo =
					       {
						       FileName = @"C:\Users\amazman\Desktop\ironbrew-2-master\IronBrew2 CLI\bin\Debug\netcoreapp2.0\luajit.exe",
						       Arguments =
							       "../Lua/Minifier/luasrcdiet.lua --noopt-whitespace --noopt-emptylines --noopt-numbers --noopt-locals --noopt-strings --opt-comments \"" +
							       input                                                       +
							       "\" -o \""                                                  + t0 + "\"",
						       UseShellExecute        = false,
						       RedirectStandardError  = true,
						       RedirectStandardOutput = true
					       }
				       };

				proc.OutputDataReceived += (sender, args) => { err += args.Data; };
				proc.ErrorDataReceived  += (sender, args) => { err += args.Data; };

				proc.Start();
				proc.BeginOutputReadLine();
				proc.BeginErrorReadLine();
				proc.WaitForExit();

				error = err;

				if (!File.Exists(t0))
					return false;

				string t1 = Path.Combine(path, "t1.lua");
				
				Console.WriteLine("Compiling...");

				File.WriteAllText(t1, new ConstantEncryption(settings, File.ReadAllText(t0, _fuckingLua)).EncryptStrings());
				proc = new Process
				       {
					       StartInfo =
					       {
						       FileName  = $"{OS}luac",
						       Arguments = "-o \"" + l + "\" \"" + t1 + "\"",
						       UseShellExecute = false,
						       RedirectStandardError = true,
						       RedirectStandardOutput = true
					       }
				       };

				proc.OutputDataReceived += (sender, args) => { err += args.Data; };
				proc.ErrorDataReceived += (sender, args) => { err += args.Data; };

				proc.Start();
				proc.BeginOutputReadLine();
				proc.BeginErrorReadLine();
				proc.WaitForExit();

				error = err;

				if (!File.Exists(l))
					return false;
				
				Console.WriteLine("Obfuscating...");

				Deserializer des    = new Deserializer(File.ReadAllBytes(l));
				Chunk lChunk = des.DecodeFile();

				if (settings.ControlFlow)
				{
					CFContext cf = new CFContext(lChunk);
					cf.DoChunks();
				}

				Console.WriteLine("Serializing...");
				
				//shuffle stuff
				//lChunk.Constants.Shuffle();
				//lChunk.Functions.Shuffle();

				ObfuscationContext context = new ObfuscationContext(lChunk);

				string t2 = Path.Combine(path, "t2.lua");
				string c = new Generator(context).GenerateVM(settings);

				//string byteLocal = c.Substring(null, "\n");
				//string rest = c.Substring("\n");

				File.WriteAllText(t2, c, _fuckingLua);

				string t3 = Path.Combine(path, "t3.lua");
				
				Console.WriteLine("Minifying...");
				
				proc = new Process
				       {
					       StartInfo =
					       {
						       FileName = $"{OS}luajit",
						       Arguments =
							       "../Lua/Minifier/luasrcdiet.lua --maximum --opt-entropy --opt-emptylines --opt-eols --opt-numbers --opt-whitespace --opt-locals --noopt-strings \"" +
							       t2                                                                                                                                                +
							       "\" -o \"" + 
							        t3 + 
							       "\""
								,
					       }
				       };

				proc.Start();
				proc.WaitForExit();

				if (!File.Exists(t3))
					return false;
				
				Console.WriteLine("Watermark...");
				
				File.WriteAllText(Path.Combine(path, "out.lua"), @"--[[
Powered by IronBrew!
]]

" + File.ReadAllText(t3, _fuckingLua).Replace("\n", " "), _fuckingLua);
				return true;
			}
			catch (Exception e)
			{
				Console.WriteLine("ERROR");
				Console.WriteLine(e);

				error = e.ToString();
				return false;
			}
		}
	}
}