#define DDEBUG
/*
 * Created by SharpDevelop.
 * User: User
 * Date: 18.06.2022
 * Time: 17:57
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;

namespace ObfProtoDecoder
{
	class Program
	{
		const char default_mode = 'd';
		const char default_type = 'b';
		
		public static void Main(string[] args)
		{
			#if DDEBUG
			var config_path = @"Z:\config_2.8.0.ini";
			var output_directory = @"Z:\pyproto";
			#else
			if (args.Length < 2)
			{
				Usage();
				return;
			}
			var config_path = args[0];
			var output_directory = args[1];
			#endif
			
			var type = default_type;
			
			if (args.Length > 2) {
				type = args[2][0];
			}
			
			if (type != 'y' && type != 'b') {
				Usage();
				return;
			}
			
			var mode = default_mode;
			
			if (args.Length > 3) {
				mode = args[3][0];
			}
			
			if (mode != 'd' && mode != 'f') {
				Usage();
				return;
			}
			
			var config = new IniReader(config_path);
			
			var obf_base_class = config.GetValue("ObfuscatedProtobufBaseClassName", "Settings");
			var obf_cmd_id = config.GetValue("ObfuscatedCmdIdName", "Settings");
			var obf_dll_path = config.GetValue("ObfuscatedDllPath", "Settings");
			
			var parser = new AssemblyParser(obf_dll_path, obf_base_class, obf_cmd_id);
			
			parser.parse();
			
			DescriptionWriter writer = null;
			
			if (type == 'b') {
				writer = new ProtobufWriter(parser.GetItems());
			} else {
				writer = new PythonPIWriter(parser.GetItems(), obf_cmd_id);
			}
			
			if (mode == 'f') {
				writer.DumpToFile(output_directory);
			} else {
				writer.DumpToDirectory(output_directory);
			}
			
			#if DDEBUG
			Console.Write("Press any key to continue . . . ");
			Console.ReadKey(true);
			#endif
		}
		
		public static void Usage() {
			var param_string = "\t{0,-15} {1}";
			
			var usage = string.Join(
				Environment.NewLine,
				"Protocol dumper tool for Unity projects (supports obfuscated assemblies)",
				"",
				"Usage:",
				string.Format("\t{0} input_config output_dir [type [mode]]", AppDomain.CurrentDomain.FriendlyName),
				"",
				"Parameters:",
				string.Format(param_string, "input_config", "Path to the config file"),
				string.Format(param_string, "output_dir", "Directory for generated files (beware of overwriting!)"),
				string.Format(param_string, "type", "Type of definitions, 'y' for pYthon pb-inspector or 'b' for protoBuf itself"),
				string.Format(param_string, "", string.Format("Defaults to '{0}'", default_type)),
				string.Format(param_string, "mode", "Either 'f' for one big File or 'd' for separate files under output Directory"),
				string.Format(param_string, "", string.Format("Defaults to '{0}'", default_mode)),
				""
			);
			Console.WriteLine(usage);
		}
	}
}