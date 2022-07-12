/*
 * Created by SharpDevelop.
 * User: User
 * Date: 18.06.2022
 * Time: 18:09
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace ObfProtoDecoder
{
	/// <summary>
	/// Description of Extension.
	/// </summary>
	public static class Extension
	{		
		public static uint GetConstant(this FieldDefinition f) {
			return UInt32.Parse(f.Constant.ToString());
		}
		
		public static bool IsBeeObfuscated(this string name) {
			// TODO: very simple but should work
			return name.All(char.IsUpper) && (name.Length >= 10 && name.Length <= 15);
		}
		
		public static string CutAfterPlusSlashAndDot(this string name) {
			int plus_pos = name.LastIndexOf('+');
			
			if (plus_pos >-1)
				name = name.Substring(plus_pos+1);
			
			int dot_pos = name.LastIndexOf('.');
			
			if (dot_pos > -1)
				name = name.Substring(dot_pos+1);
			
			int slash_pos = name.LastIndexOf('/');
			
			if (slash_pos > -1)
				name = name.Substring(slash_pos+1);
			
			return name;
		}
		
		public static string[] PadStrings(this string[] lines, string left_pad = "\t", string right_pad = "")
		{
			var ret = new List<string>();
			
			foreach (var line in lines)
				ret.Add(left_pad + line + right_pad);
			
			return ret.ToArray();
		}
		
		// Stolen from EF Core
		public static string ToSnakeCase(this string name)
		{
			if (string.IsNullOrEmpty(name))
				return name;

			var builder = new StringBuilder(name.Length + Math.Min(2, name.Length / 5));
			var previousCategory = default(UnicodeCategory?);

			for (var currentIndex = 0; currentIndex < name.Length; currentIndex++) {
				var currentChar = name[currentIndex];
				if (currentChar == '_') {
					builder.Append('_');
					previousCategory = null;
					continue;
				}

				var currentCategory = char.GetUnicodeCategory(currentChar);
				switch (currentCategory) {
					case UnicodeCategory.UppercaseLetter:
					case UnicodeCategory.TitlecaseLetter:
						if (previousCategory == UnicodeCategory.SpaceSeparator ||
						      previousCategory == UnicodeCategory.LowercaseLetter ||
						      previousCategory != UnicodeCategory.DecimalDigitNumber &&
						      previousCategory != null &&
						      currentIndex > 0 &&
						      currentIndex + 1 < name.Length &&
						      char.IsLower(name[currentIndex + 1])) {
							builder.Append('_');
						}

						currentChar = char.ToLower(currentChar);
						break;

					case UnicodeCategory.LowercaseLetter:
					case UnicodeCategory.DecimalDigitNumber:
						if (previousCategory == UnicodeCategory.SpaceSeparator)
							builder.Append('_');
						break;

					default:
						if (previousCategory != null)
							previousCategory = UnicodeCategory.SpaceSeparator;
						continue;
				}

				builder.Append(currentChar);
				previousCategory = currentCategory;
			}

			return builder.ToString();
		}
	}
}
