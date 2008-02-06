// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="David Srbeck�" email="dsrbecky@gmail.com"/>
//     <version>$Revision$</version>
// </file>

using System;

namespace Debugger
{
	/// <summary>
	/// Unique identifier of the state of the debugee.
	/// Changes when debuggee is stepped, but not when properity is evaluated.
	/// </summary>
	public class DebuggeeState: IExpirable
	{
		Process process;
		bool hasExpired = false;
		
		public event EventHandler Expired;
		
		[Debugger.Tests.Ignore]
		public Process Process {
			get {
				return process;
			}
		}
		
		public bool HasExpired {
			get {
				return hasExpired;
			}
		}
		
		internal void NotifyHasExpired()
		{
			if(!hasExpired) {
				hasExpired = true;
				if (Expired != null) {
					Expired(this, EventArgs.Empty);
				}
			}
		}
		
		public DebuggeeState(Process process)
		{
			this.process = process;
		}
	}
}