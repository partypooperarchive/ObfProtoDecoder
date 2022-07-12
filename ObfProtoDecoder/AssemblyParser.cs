/*
 * Created by SharpDevelop.
 * User: User
 * Date: 18.06.2022
 * Time: 18:02
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
	/// Description of AssemblyParser.
	/// </summary>
	public class AssemblyParser
	{
		private const string token_attrib_name = "TokenAttribute";
		
		private string obf_base_class_name = null;
		
		private string obf_cmd_id_name = null;
		
		AssemblyDefinition obf_assembly = null;
		
		Dictionary<uint, TypeDefinition> obf_types = null;
		
		Dictionary<string,ObjectDescription> items = null;
		
		public AssemblyParser(string filename, string obf_base_class, string obf_cmd_id)
		{
			obf_cmd_id_name = obf_cmd_id;
			
			load_obf_assembly(filename, obf_base_class, obf_cmd_id);
		}
		
		public Dictionary<string,ObjectDescription> GetItems()
		{
			return items;
		}
		
		public void parse()
		{
			items = new Dictionary<string, ObjectDescription>();
			
			foreach (var kvp in obf_types)
			{
				var cmd_id = kvp.Key;
				var type = kvp.Value;
				
				if (!IsParsed(type)) {
					items.Add(type.Name, parseType(type));
				}
			}
			
			foreach (var od in items.Values.ToList()) {
				var cd = od as ClassDescription;
				
				if (cd != null) {
					var e_types = cd.GetExternalTypes();
					
					foreach (var e_type in e_types) {
						var ed_type = e_type.Resolve();
						
						if (!IsParsed(ed_type)) {
							items.Add(ed_type.Name, parseType(ed_type));
						}
					}
				}
			}
		}
		
		private bool IsParsed(TypeDefinition type) {
			return items.ContainsKey(type.Name);
		}
		
		private ObjectDescription parseType(TypeDefinition type) {			
			return type.IsEnum ? (ObjectDescription)parseEnum(type) : (ObjectDescription)parseClass(type);
		}
		
		private ClassDescription parseClass(TypeDefinition type) {			
			if (type.Name.EndsWith("MaterialDeleteInfo")) {
				Console.WriteLine("Poo");
			}
			
			ClassDescription cd = new ClassDescription(type.FullName, type);
			
			var inner_types = get_public_nested_types_recursively(type); //type.NestedTypes.Where(t => t.IsPublic);
			
			foreach (var inner_type in inner_types) {
				var od = parseType(inner_type);
				if (inner_type.IsEnum) {
					cd.Enums.Add(inner_type.Name, od as EnumDescription);
				} else {
					cd.Classes.Add(inner_type.Name, od as ClassDescription);
				}
			}
			
			var fields = get_protobuf_fields(type);
			
			foreach (var kvp in fields) {
				var f_tag = kvp.Key;
				var f_field = kvp.Value;
				
				// TODO: Check for map (2-arg generic)
				// TODO: Check for repeated (1-arg generic)
				
				cd.Fields.Add(f_field.Name, f_field);
			}
			
			var cmd_id_enum = GetProtobufPacketEnum(type, obf_base_class_name, obf_cmd_id_name);
			
			if (cmd_id_enum != null)
				cd.ServiceEnum = parseEnum(cmd_id_enum);
			
			Console.WriteLine("Processed class " + type.FullName);
			
			return cd;
		}
		
		private EnumDescription parseEnum(TypeDefinition t)
		{
			EnumDescription ed = new EnumDescription(t.FullName);
			
			var fields = t.Fields.Where(f => !f.Name.Equals("value__"));
			
			foreach (var f in fields) {
				var value = f.GetConstant();
				
				ItemDescription od = new ItemDescription(f.Name, (int)value);
				
				ed.Items.Add(f.Name, od);
			}
			
			return ed;
		} 
		
		private Dictionary<uint, TypeDefinition> load_pb_packet_types_from_assembly(AssemblyDefinition asm, string pb_base_class, string pb_cmd_id) {			
			var types = GetProtobufPacketTypes(asm, pb_base_class, pb_cmd_id);
			
			var types_dict = new Dictionary<uint, TypeDefinition>();
			
			uint i = 0;
			
			foreach (var type in types)
			{
				//var cmd_id = GetCmdId(type, pb_cmd_id);
				//var token = GetToken(type);
				var cmd_id = i++;
				try {
					types_dict.Add(cmd_id, type);
				} catch (ArgumentException e) {
					var en = GetProtobufPacketEnum(type, pb_base_class, pb_cmd_id);
					// TODO: dirty hack!
					var constants = en.Fields.Where(f => f.HasConstant).Select(f => f.GetConstant()).ToList();
					var is_debug = constants.Contains(2);
					if (is_debug) {
						// This is DebugNotify packet, ignore it
					} else {
						// DebugNotify came first, we need to overwrite it
						types_dict[cmd_id] = type;
					}
				}
			}
			
			return types_dict;
		}
		
		private SortedDictionary<uint, FieldDescription> get_protobuf_fields(TypeReference t) {	
			//if (t.Name.EndsWith("SceneEntityInfo"))
			if (t.Name.EndsWith("GalleryStartNotify"))
				Console.WriteLine();
			
			var type = t.Resolve();
			
			// Enumerate all fields and select pairs public-private (id - value) with token for IDs
			var result = new SortedDictionary<uint, FieldDescription>();
			
			var fields = type.Fields.OrderBy(x => GetToken(x)).ToList();
			
			if (fields.Count == 0)
				return result; // Nothing to do here
			
			//uint base_token = fields.Select(f => GetFieldToken(f)).Min(); // Min will throw if collection is empty, but we checked it just above
			
			uint field_seq_number = 0;
			int prop_seq_number = 0;
			
			var properties = type.Properties.Where(p => p.HasThis /* not static */ && !p.GetMethod.IsVirtual /* doesn't have 'override' spec */).OrderBy(x => GetToken(x)).ToList();
			
			// For oneofs: find all corresponding enums
			// See comment about oneofs below
			List<TypeDefinition> oneof_enums = new List<TypeDefinition>();
			
			for (int i = 0; i < fields.Count; i++) {
				var f = fields[i];
				if (f.FieldType.FullName.Equals(typeof(object).FullName)) {
					var en_f = fields[i+1];
					var en = en_f.FieldType.Resolve();
					if (!en.IsEnum)
						throw new ArgumentException(string.Format("Your assumption is fucked up: type {0}, subtype {1}", type.Name, en.Name));
					oneof_enums.Add(en);
					i++;
				}
			}
			
			int current_oneof = 0;
			
			for (int i = 0; i < fields.Count-1; i++) {
				var f1 = fields[i];
				
				if (is_protobuf_field(f1)) {
					// Protobuf ID
					var f2 = fields[i+1];
					
					// This hack is a workaround for corner-case: oneof with one element is last in the list of fields
					var hack = (current_oneof == oneof_enums.Count-1) &&
						f2.FieldType.FullName.Equals(typeof(object).FullName) &&
						(prop_seq_number == properties.Count-2);
					
					if (f2.IsPrivate && !f2.HasConstant && !hack) {
						// Regular field
						var ft = f2.FieldType;
						
						if (ft.IsGenericInstance) {
							i++; // There're two fields of generic type, one is FieldCodec, the other one is actual data
						}
						
						result.Add(field_seq_number++, new RegularField(f1.Name, f1.GetConstant(), ft));
						i++;
						prop_seq_number++;
					} else if (is_protobuf_field(f2) || hack) {
						// And now it's time for some BLACK MAGIC
						// Sequence of protobuf fields that follow each other without delimiting type fields mean OneOf field
						// It is followed by the regular field, so we can't just read everything sequentially
						// But this "oneof" has:
						// 1. Corresponding "object" field (no other fields has that).
						// 2. Corresponding field with enum type right after this "object" field.
						// So we should find corresponding enum (just by index) and read as many values as there's items in the enum
						// This enum's values also seem to be unobfuscated (lucky us!)
						
						// HACK: if current_oneof >= oneof_enums.Count, that means it's not really a field but just a constant (i.e. end of fields)
						if (current_oneof >= oneof_enums.Count)
							break;
						
						// Read all variants
						var oneof_enum = oneof_enums[current_oneof++];
						var oneof_variants = new List<FieldDefinition>();
						for (int j = 0; j < oneof_enum.Fields.Count-2; j++) // First field is '__value', next is 'None'
						{
							oneof_variants.Add(fields[i+j]);
						}	

						# if false						
						// We can guess the types of elements in the following way:
						// 1. Find a property linked to this particular field - it has the same type
						int idx = -1;
						
						for (int j = 0; j < properties.Count; j++) {
							if (properties[j].PropertyType.Equals(oneof_enum)) {
							    idx = j;
							    break;
							}
						}
						
						if (idx < 0)
							throw new ArgumentException(string.Format("Failed to find property for oneof {0} (type {1})", oneof_enum.Name, type.Name));
						
						//WriteLine("Enum: {0}", oneof_enum);
						
						// 2. Going from that property position - N, go through the properties list and retrieve types of corresponding properties
						var oneof_field = new OneofField(oneof_enum.Name);
						
						for (int j = 0; j < oneof_variants.Count; j++) {
							var prop = properties[idx - oneof_variants.Count + j];
							var pt = prop.PropertyType;
							
							// Bonus magic trick: rename obfuscated types and fields based on enum variants
							// This doesn't really matter because we won't use obfuscated assembly, only deobfuscated one
							// But still, maybe one day...
							var enum_var_name = oneof_enum.Fields[j + 2].Name; // First field is '__value', next is 'None'
							
							if (pt.FullName.IsBeeObfuscated() && !enum_var_name.IsBeeObfuscated()) {
								fields[i].Name = enum_var_name + "FieldNumber";
								prop.Name = enum_var_name;
								pt.Name = enum_var_name;
							}
							
							oneof_field.AddRecord(oneof_variants[j], pt.Resolve());
						}
						#else 
						var oneof_field = new OneofField(oneof_enum.Name);
						
						for (int j = 0; j < oneof_variants.Count; j++) {
							var prop = properties[prop_seq_number + j];
							var pt = prop.PropertyType;
							
							// Bonus magic trick: rename obfuscated types and fields based on enum variants
							// This doesn't really matter because we won't use obfuscated assembly, only deobfuscated one
							// But still, maybe one day...
							var enum_var_name = oneof_enum.Fields[j + 2].Name; // First field is '__value', next is 'None'
							
							if (!enum_var_name.IsBeeObfuscated()) {
								if (pt.FullName.IsBeeObfuscated()) {
									pt.Name = enum_var_name;
								}
								
								if (fields[i+j].Name.IsBeeObfuscated()) {
									fields[i+j].Name = enum_var_name + "FieldNumber";
								}
								
								if (prop.Name.IsBeeObfuscated()) {
									prop.Name = enum_var_name;
								}
							}
							
							oneof_field.AddRecord(oneof_variants[j], pt.Resolve());
						}						
						#endif
						
						//WriteLine("Loaded {0} oneof variants", oneof_variants.Count);
						result.Add(field_seq_number++, oneof_field);
						i += oneof_variants.Count-1;
						prop_seq_number += oneof_variants.Count;
					} else {
						throw new ArgumentException(string.Format("Incorrect field {0} follows {1} in {2}", f2.Name, f1.Name, type.Name));
					}
				}
			}
			
			return result;
		}
		
		public void load_obf_assembly(string filename, string obf_base_class, string obf_cmd_id) {
			obf_base_class_name = obf_base_class;

			obf_assembly = load_assembly(filename);
			
			obf_types = load_pb_packet_types_from_assembly(obf_assembly, obf_base_class, obf_cmd_id);
		}
		
		private AssemblyDefinition load_assembly(string filename) {
			var resolver = new DefaultAssemblyResolver();
			resolver.AddSearchDirectory(Path.GetDirectoryName(filename));
			
			return AssemblyDefinition.ReadAssembly(filename, new ReaderParameters { AssemblyResolver = resolver });
		}
		
		private TypeDefinition[] GetTypes(AssemblyDefinition assembly)
		{
			return assembly.MainModule.Types.ToArray();
		}
		
		private bool IsProtobufPacket(TypeDefinition t, string base_class_name, string cmd_id_field_name)
		{
			//this was the case for 1.4.51, but sadly not anymore
			//return t.FullName.StartsWith("Proto.") && !t.FullName.Contains("+");
			
			//return GetProtobufPacketEnum(t, base_class_name, cmd_id_field_name) != null;
			return t.BaseType != null && t.BaseType.FullName.Equals(base_class_name);
		}
		
		private TypeDefinition[] GetProtobufPacketTypes(AssemblyDefinition assembly, string base_class_name, string cmd_id_field_name)
		{
			return GetTypes(assembly).OrderBy(t => t.Name).Where(t => IsProtobufPacket(t, base_class_name, cmd_id_field_name)).ToArray();
		}
		
		private uint GetCmdId(TypeDefinition t, string cmd_id_field_name)
		{
			foreach (var nested_type in t.NestedTypes)
			{
				foreach (var inner_type in nested_type.NestedTypes)
				{
					if (inner_type.IsEnum) 
					{
						foreach (var field in inner_type.Fields)
						{
							if (field.Name == cmd_id_field_name)
								return field.GetConstant();
						}
					}
				}
			}
			
			throw new ArgumentException();
		}
		
		private TypeDefinition GetProtobufPacketEnum(TypeDefinition t, string base_class_name, string cmd_id_field_name) {
			var base_type = t.BaseType;
			
			if (base_type == null || base_type.FullName.Split('.').Last() != base_class_name)
				return null;
			
			// There should exist nested type with nested enum with element "CmdId"
			foreach (var nested_type in t.NestedTypes)
			{
				foreach (var inner_type in nested_type.NestedTypes)
				{
					if (inner_type.IsEnum) 
					{
						foreach (var field in inner_type.Fields)
						{
							if (field.Name == cmd_id_field_name)
								return inner_type;
						}
					}
				}
			}
			
			return null;
		}
		
		private uint GetToken(IMemberDefinition f)
		{
			foreach (var attrib in f.CustomAttributes)
			{
				if (attrib.AttributeType.Name == token_attrib_name)
				{
					var token = attrib.Fields[0].Argument.Value.ToString();
					return Convert.ToUInt32(token, 16);
				}
			}
			
			throw new ArgumentException();
		}
		
		private bool is_protobuf_field(FieldDefinition f) {
			return f.IsPublic && f.HasConstant && f.IsStatic && f.FieldType.FullName.Equals(typeof(int).FullName);
		}
		
		private IEnumerable<TypeDefinition> get_public_nested_types_recursively(TypeDefinition type) {
			var nested_types = new List<TypeDefinition>();
			
			foreach (var t in type.NestedTypes) {
				nested_types.AddRange(get_public_nested_types_recursively(t));
			}
			
			nested_types.AddRange(type.NestedTypes.Where(t => IsProtobufPacket(t, obf_base_class_name, null)));
			
			return nested_types;
		}
	}
}
