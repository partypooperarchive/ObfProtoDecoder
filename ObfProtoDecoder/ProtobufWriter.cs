/*
 * Created by SharpDevelop.
 * User: User
 * Date: 24.03.2021
 * Time: 11:21
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.IO;

namespace ObfProtoDecoder
{
	/// <summary>
	/// Description of ProtobufWriter.
	/// </summary>
	public class ProtobufWriter : DescriptionWriter
	{		
		public ProtobufWriter(Dictionary<string, ObjectDescription> items) : base(items)
		{
		}
		
		public override void DumpToFile(string path) {
			var filename = Path.Combine(path, "protocol.proto");
			
			var w = new StreamWriter(filename);
			
			w.WriteLine("syntax = \"proto3\";");
			
			w.WriteLine();
			
			w.WriteLine("package Proto;");
			
			foreach (var item in Items) {
				DumpItem(w, item.Value);
			}
			w.WriteLine();
		}
		
		public override void DumpToDirectory(string directory) {
			foreach (var item in Items) {
				var filename = Path.Combine(directory, item.Key.CutAfterPlusSlashAndDot() + ".proto");
				
				var w = new StreamWriter(filename);
				
				w.WriteLine("syntax = \"proto3\";");
				
				w.WriteLine();
			
				w.WriteLine("package Proto;");
				
				w.WriteLine();
				
				DumpItem(w, item.Value);
				
				w.Close();
			}
		}
		
		protected void DumpItem(StreamWriter w, ObjectDescription item)
		{
			var lines = item.ToPBLines();
				
			var c = item as ClassDescription;
				
			if (c != null) {
				// Imports
				foreach (var t in c.GetExternalTypes()) {
					var cut = t.FullName.CutAfterPlusSlashAndDot();
					
					if (cut.Length == 0)
					{
						throw new Exception("PooPee: " + t);
					}
					
					w.WriteLine("import \"{0}.proto\";", cut);
				}
				w.WriteLine();
			}
				
			foreach (var line in lines) {
				w.WriteLine(line);
			}
		}
	}
}
