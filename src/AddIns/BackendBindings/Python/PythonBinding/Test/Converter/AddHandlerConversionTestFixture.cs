﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using ICSharpCode.NRefactory;
using ICSharpCode.PythonBinding;
using NUnit.Framework;

namespace PythonBinding.Tests.Converter
{
	/// <summary>
	/// Tests that assigning a method to an event handler is converted
	/// from C# to Python correctly.
	/// </summary>
	[TestFixture]
	public class AddHandlerConversionTestFixture
	{		
		string csharp = "class Foo\r\n" +
						"{\r\n" +
						"\tpublic Foo()\r\n" +
						"\t{" +
						"\t\tbutton = new Button();\r\n" +
						"\t\tbutton.Click += ButtonClick;\r\n" +
						"\t\tbutton.MouseDown += self.OnMouseDown;\r\n" +
						"\t}\r\n" +
						"\r\n" +
						"\tvoid ButtonClick(object sender, EventArgs e)\r\n" +
						"\t{\r\n" +
						"\t}\r\n" +
						"\r\n" +
						"\tvoid OnMouseDown(object sender, EventArgs e)\r\n" +
						"\t{\r\n" +
						"\t}\r\n" +
						"}";
				
		[Test]
		public void ConvertedPythonCode()
		{
			string expectedCode = "class Foo(object):\r\n" +
									"\tdef __init__(self):\r\n" +
									"\t\tbutton = Button()\r\n" +
									"\t\tbutton.Click += self.ButtonClick\r\n" +
									"\t\tbutton.MouseDown += self.OnMouseDown\r\n" +
									"\r\n" +
									"\tdef ButtonClick(self, sender, e):\r\n" +
									"\t\tpass\r\n" +
									"\r\n" +
									"\tdef OnMouseDown(self, sender, e):\r\n" +
									"\t\tpass";
			NRefactoryToPythonConverter converter = new NRefactoryToPythonConverter(SupportedLanguage.CSharp);
			string code = converter.Convert(csharp);
			
			Assert.AreEqual(expectedCode, code);
		}
	}
}
