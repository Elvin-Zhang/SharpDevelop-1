﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections;

namespace ICSharpCode.NRefactory.Parser.AST
{
	public abstract class Statement : AbstractNode, INullable
	{
		public static NullStatement Null {
			get {
				return NullStatement.Instance;
			}
		}
		
		public virtual bool IsNull {
			get {
				return false;
			}
		}
		
		public static Statement CheckNull(Statement statement)
		{
			return statement == null ? NullStatement.Instance : statement;
		}
		
		public static void Replace(Statement oldStatement, Statement newStatement)
		{
			INode parent = oldStatement.Parent;
			StatementWithEmbeddedStatement parentStmt = parent as StatementWithEmbeddedStatement;
			if (parentStmt != null && parentStmt.EmbeddedStatement == oldStatement)
				parentStmt.EmbeddedStatement = newStatement;
			int index = parent.Children.IndexOf(oldStatement);
			if (index >= 0) {
				parent.Children[index] = newStatement;
				newStatement.Parent = parent;
			}
		}
	}
	
	public abstract class StatementWithEmbeddedStatement : Statement
	{
		Statement embeddedStatement;
		
		public Statement EmbeddedStatement {
			get {
				return embeddedStatement;
			}
			set {
				embeddedStatement = Statement.CheckNull(value);
				if (value != null)
					value.Parent = this;
			}
		}
	}
	
	public class NullStatement : Statement
	{
		static NullStatement nullStatement = new NullStatement();
		
		public override bool IsNull {
			get {
				return true;
			}
		}
		
		public static NullStatement Instance {
			get {
				return nullStatement;
			}
		}
		
		NullStatement()
		{
		}
		
		public override object AcceptVisitor(IAstVisitor visitor, object data)
		{
			return data;
		}
		
		public override string ToString()
		{
			return String.Format("[NullStatement]");
		}
	}
}
