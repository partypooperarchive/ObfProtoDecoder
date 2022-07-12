/*
 * Created by SharpDevelop.
 * User: User
 * Date: 24.03.2021
 * Time: 11:56
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;

namespace ObfProtoDecoder
{
	/// <summary>
	/// Description of DescriptionWriter.
	/// </summary>
	public abstract class DescriptionWriter
	{
		protected Dictionary<string,ObjectDescription> Items = null;
		
		public DescriptionWriter(Dictionary<string,ObjectDescription> items)
		{
			Items = items;
		}
		
		public abstract void DumpToFile(string filename);
		public abstract void DumpToDirectory(string directory);
		
	}
}
