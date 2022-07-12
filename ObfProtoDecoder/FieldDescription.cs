/*
 * Created by SharpDevelop.
 * User: User
 * Date: 23.03.2021
 * Time: 9:37
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Mono.Cecil;

namespace ObfProtoDecoder
{
	/// <summary>
	/// Description of FieldDescription.
	/// </summary>
	public abstract class FieldDescription : ObjectDescription
	{			
		private static Dictionary<string, string> pbTypeNames = new Dictionary<string, string>() {
			{typeof(System.UInt32).FullName, "uint32"},
			{typeof(System.UInt64).FullName, "uint64"},
			{typeof(System.Int32).FullName, "int32"},
			{typeof(System.Int64).FullName, "int64"},
			{typeof(System.Boolean).FullName, "bool"},
			{typeof(System.String).FullName, "string"},
			{typeof(float).FullName, "float"},
			{typeof(double).FullName, "double"},
			{"Google.Protobuf.ByteString", "bytes"},
		};
		
		public FieldDescription(string name) : base(name)
		{
		}
		
		public static string MapCsTypeToPb(TypeReference type, bool annotate_enums = false, bool add_repeated = true)
		{						
			if (pbTypeNames.ContainsKey(type.FullName))
				return pbTypeNames[type.FullName];
			
			var gt = type as GenericInstanceType;
			
			if (/*typename.StartsWith("Google.Protobuf.Collections.Repeated")*/gt != null && gt.GenericArguments.Count == 1) {
				/*var element_type = typename.Split('<')[1];
				
				element_type = element_type.Substring(0, element_type.Length-1);*/
				
				var element_type = gt.GenericArguments[0];
				
				var type_name = MapCsTypeToPb(element_type, annotate_enums);
				
				if (add_repeated) {
					return "repeated " + type_name;
				} 
				
				if (pbTypeNames.ContainsKey(element_type.FullName)) {
					// This is a primitive type, and it should be packed for protobuf-inspector
					return "packed " + type_name;
				}
				
				return type_name;
			}
			
			if (/*typename.StartsWith("Google.Protobuf.Collections.MapField") ||
			    typename.StartsWith("Google.Protobuf.Collections.MessageMapField")*/
			   gt != null && gt.GenericArguments.Count == 2) {
				/*var types_part = typename.Split('<');
				var types = types_part[1].Split(',');*/
				
				var key_type = MapCsTypeToPb(gt.GenericArguments[0], annotate_enums);
				var value_type = MapCsTypeToPb(gt.GenericArguments[1], annotate_enums);
				
				return string.Format("map<{0}, {1}>", key_type, value_type);
			}
			
			var name = type.FullName.CutAfterPlusSlashAndDot();
			
			// HACK!
			if (annotate_enums && type.Resolve().IsEnum)
				return "enum " + name;
			
			return name; // And pray for the best
		}
	}
	
	public class RegularField: FieldDescription 
	{
		private uint field_number = 0;
		private TypeReference field_type = null;
		
		public override TypeReference Type {
			get {
				return field_type;
			}
		}
		
		public RegularField(string name, uint field_number, TypeReference field_type) : base(name)
		{
			this.field_number = field_number;
			this.field_type = field_type;
		}
		
		public override string[] ToPBLines()
		{
			return new string[]{MapCsTypeToPb(field_type) + " " + GetName() + " = " + field_number};
		}
		
		public override string[] ToPILines()
		{
			// TODO: type mapping!
			return new string[]{field_number + ": (\"" + MapCsTypeToPb(field_type, true, false) + "\", \"" + GetName() + "\")"};
		}
		
		public string GetName() {
			return Name.ToSnakeCase().Replace("_field_number", "");
		}
	}
	
	public class OneofField: FieldDescription
	{
		public Dictionary<int, RegularField> Fields = null;
		
		public override TypeReference Type {
			get {
				throw new InvalidOperationException();
			}
		}
		
		public OneofField(string name) : base(name)
		{
			Fields = new Dictionary<int, RegularField>();
		}
		
		public void AddRecord(FieldDefinition field_number, TypeReference field_type)
		{
			int index = Fields.Count;
			var tuple = new RegularField(field_number.Name, field_number.GetConstant(), field_type);
			Fields.Add(index, tuple);
		}
		
		public string GetName() {
			return Name.CutAfterPlusSlashAndDot().Replace("OneofCase", "");
		}
		
		public override string[] ToPBLines() {
			var lines = new List<string>();
			
			lines.Add("oneof " + GetName() + " {");
			
			foreach (var item in Fields)
			{
				lines.AddRange(item.Value.ToPBLines().PadStrings("\t", ";"));
			}
			
			lines.Add("}");
			
			return lines.ToArray();
		}
		
		public override string[] ToPILines() {
			// Oneofs aren't directly supported by protobuf-inspector, so we'll just put comments marking it's start/end
			var lines = new List<string>();
			
			lines.Add("# oneof " + GetName() + " {");
			
			foreach (var item in Fields)
			{
				// Note that we don't want to pad it
				lines.AddRange(item.Value.ToPILines().PadStrings(""));
			}
			
			lines.Add("# }");
			
			return lines.ToArray();
		}
	}
}
