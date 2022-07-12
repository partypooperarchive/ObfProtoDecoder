/*
 * Created by SharpDevelop.
 * User: User
 * Date: 24.03.2021
 * Time: 12:00
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using System.Linq;

namespace ObfProtoDecoder
{
	/// <summary>
	/// Description of PythonPIWriter.
	/// </summary>
	public class PythonPIWriter : DescriptionWriter
	{		
		private static HashSet<string> enumNames = new HashSet<string>();
		
		private string cmd_id_name = null;
		
		public PythonPIWriter(Dictionary<string, ObjectDescription> items, string cmd_id_name) : base(items)
		{
			this.cmd_id_name = cmd_id_name;
		}
		
		public override void DumpToFile(string directory) {
			Dump(directory, false);
		}
		
		public override void DumpToDirectory(string directory) {
			Dump(directory, true);
		}
		
		protected void Dump(string path, bool split) {
			const int channelsCount = 3;
			
			var packet_map = new IDictionary<int,string>[channelsCount];
			
			for (int i = 0; i < channelsCount; i++)
				packet_map[i] = new SortedDictionary<int,string>();
			
			// HACK: first we need to collect all enums and populate corresponding dict
			enumNames.Clear();
			
			foreach (var item in Items) {
				var e = item.Value as EnumDescription;
				
				if (e != null) {
					enumNames.Add(e.Name);
				}
				
				var c = item.Value as ClassDescription;
				
				if (c != null) {
					foreach (var name in c.GetEnums()) {
						enumNames.Add(name);
					}
				}
			}
			
			// Writer for common classes
			StreamWriter cw = null;
			
			cw = CreatePythonWriter(path, "common.py");
		
			foreach (var item in Items) {
				var w = cw;
				
				var c = item.Value as ClassDescription;
				
				if (c != null && c.ServiceEnum != null && c.ServiceEnum.Items.ContainsKey(this.cmd_id_name)) {
					if (split)
						w = CreatePythonWriter(path, item.Key.CutAfterPlusSlashAndDot() + ".py");
					
					var cmdId = c.ServiceEnum.Items[this.cmd_id_name].Value;
					var channelId = -1;
						
					try {
						channelId = c.ServiceEnum.Items["EnetChannelId"].Value;
					} catch (KeyNotFoundException e) {
						Console.WriteLine("WARNING: Class {0} doesn't specify it's ENet Channel ID; assigning default value 0", c.Name);
						channelId = 0;
					}
					
					var types_dict = packet_map[channelId];
					
					//packet_map[channelId].Add(cmdId, item.Key.CutAfterPlusAndDot());
					try {
						types_dict.Add(cmdId, item.Key.CutAfterPlusSlashAndDot());
					} catch (ArgumentException e) {
						// TODO: dirty hack!
						var constants = c.ServiceEnum.Items.Values.Select(f => f.Value).ToList();
						var is_debug = constants.Contains(2);
						if (is_debug) {
							// This is DebugNotify packet, ignore it
						} else {
							// DebugNotify came first, we need to overwrite it
							types_dict[cmdId] = item.Key.CutAfterPlusSlashAndDot();
						}
					}
				}
				
				DumpItem(w, item.Value);
				
				if (w != cw) {
					w.WriteLine("}");
					w.WriteLine("");
					w.Close();
				}
			}
			
			WriteGoogleTypes(cw);
			
			cw.WriteLine("}");
			cw.WriteLine("");
			cw.Close();
			
			// Write packet mapping
			cw = CreatePythonWriter(path, "packets.py");
			
			for (int i = 0; i < channelsCount; i++) {
				cw.WriteLine("\t{0}: {{", i);
				
				foreach (var item in packet_map[i]) {
					cw.WriteLine("\t\t{0,4}: \"{1}\",", item.Key, item.Value);
				}
				
				cw.WriteLine("\t},");
			}
			
			cw.WriteLine("}");
			cw.WriteLine("");
			cw.Close();
		}
		
		protected StreamWriter CreatePythonWriter(string path, string filename = null)
		{
			var full_path = path;
			
			if (filename != null)
				full_path = Path.Combine(path, filename);
			
			var w = new StreamWriter(full_path);
			
			w.WriteLine("#!/usr/bin/env python3");
			
			w.WriteLine();
			
			w.WriteLine("types = {");
			
			return w;
		}
		
		protected void DumpItem(StreamWriter w, ObjectDescription item)
		{
			var lines = item.ToPILines().PadStrings();
			
			// We don't need imports right now, but maybe in the future...
			#if false
			var c = item as ClassDescription;
			
			if (c != null) {
				// Imports
				foreach (var t in c.GetExternalTypes()) {
					var cut = t.CutAfterPlusAndDot();
					
					if (cut.Length == 0)
					{
						throw new Exception("PooPee: " + t);
					}
					
					w.WriteLine("from definitions.{0} import {0}", cut);
				}
				w.WriteLine();
			}
			#endif
				
			foreach (var line in lines) {
				w.WriteLine(line);
			}
		}
		
		protected void WriteGoogleTypes(StreamWriter w)
		{
			// Because protobuf-inspector doesn't support maps, we need to create types that'll be used as their substitute
			
			// First, gather all maps from our classes
			var maps = new HashSet<string>();
			
			foreach (var item in Items) {
				var c = item.Value as ClassDescription;
				
				if (c != null) {
					var types = c.GetExternalTypes(true);
					
					foreach (var type in types) {
						if (/*type.FullName.StartsWith("Google.Protobuf.Collections.MapField")*/ 
						    type.IsGenericInstance && (type as GenericInstanceType).GenericArguments.Count == 2) {
							maps.Add(FieldDescription.MapCsTypeToPb(type, true));
						}
					}
				}
			}
			
			// Next, actually create our type
			foreach (var type in maps) {
				w.WriteLine("\t\"{0}\": {{", type);
				
				int pos = type.IndexOf('<');
				var types_string = type.Substring(pos+1, type.Length-pos-2);
				var types_arr = types_string.Split(',');
				
				w.WriteLine("\t\t1: (\"{0}\", \"key\"),", types_arr[0].Trim());
				w.WriteLine("\t\t2: (\"{0}\", \"value\"),", types_arr[1].Trim());
				
				w.WriteLine("\t},");
				w.WriteLine();
			}
		}
		
		public static bool IsEnum(string typename)
		{
			return enumNames.Contains(typename);
		}
	}
}
