// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved. 

// get past MouseData not being initialized warning...it needs to be there for p/invoke
#pragma warning disable 0649

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace KinectCursorController
{
    // The struct that contains all(!) the information associated with the mouse
	internal struct MouseInput
	{
		public int X;   // The mouse's absolute X coordinate
		public int Y;   // The mouse's absolute Y coordinate
		public uint MouseData;  // If Flags contains a flag for the mouse wheel, this provides the amount of wheel movement
		public uint Flags;      // Contains various flags relative to the mouse event
		public uint Time;       // The timestamp on the mouse event
		public IntPtr ExtraInfo;    // This is pretty much ignored
	}

    // The struct that actually contains all the information associated with a mouse event.
    // This time the mouse event type is included (god-only-knows why we need a struct within a struct)
	internal struct Input
	{
		public int Type;    // Presumably the mouse event type
		public MouseInput MouseInput;   // Instantiating a struct containing all the mouse infor
	}

	public static class NativeMethods
	{
		public const int InputMouse = 0;

        // Looks like encoding of the mouse event types into macros?
		public const int MouseEventMove      = 0x01;
		public const int MouseEventLeftDown  = 0x02;
		public const int MouseEventLeftUp    = 0x04;
		public const int MouseEventRightDown = 0x08;
		public const int MouseEventRightUp   = 0x10;
		public const int MouseEventAbsolute  = 0x8000;

		// 
        private static bool lastLeftDown;

		[DllImport("user32.dll", SetLastError = true)]
		private static extern uint SendInput(uint numInputs, Input[] inputs, int size);


		public static void SendMouseInput(
            int positionX,  // The mouse's x position, set by the right hand position and mousespeed
            int positionY,  // The mouse's y position, ditto
            int maxX,       // The maximum screen width
            int maxY,       // The maximum screen height
            bool leftDown   // Check on whether the user tried to click
            )
		{
            // Checks on validity of incoming data (that the cursor is within the range of the screen)
			if(positionX > int.MaxValue)
				throw new ArgumentOutOfRangeException("positionX");
			if(positionY > int.MaxValue)
				throw new ArgumentOutOfRangeException("positionY");

			// WHY IS THIS AN ARRAY, WHY DO WE NEED TWO INPUTS?
            Input[] i = new Input[2];

			// move the mouse to the position specified
			i[0] = new Input();
			i[0].Type = InputMouse;
            i[0].MouseInput.X = (positionX * 65535) / maxX; // 65535 is the limit of the absolute coordinates
			i[0].MouseInput.Y = (positionY * 65535) / maxY; // 0,0 being the upper left corner, 65535,65535 being the lower left
			i[0].MouseInput.Flags = MouseEventAbsolute | MouseEventMove; // "|" includes both flags even if first one is already true

			// Determine if we need to flag a mouse down or mouse up event:
            //
            // Checks whether the we went from not-down to down
			if(!lastLeftDown && leftDown)
			{
				i[1] = new Input();
				i[1].Type = InputMouse;
				i[1].MouseInput.Flags = MouseEventLeftDown;
			}
            // Checks whether we went from down to not-down
			else if(lastLeftDown && !leftDown)
			{
				i[1] = new Input();
				i[1].Type = InputMouse;
				i[1].MouseInput.Flags = MouseEventLeftUp;
			}

            // Cache the last left down value for future checks
			lastLeftDown = leftDown;

			// send it off
			uint result = SendInput(2, i, Marshal.SizeOf(i[0]));
			if(result == 0)
				throw new Win32Exception(Marshal.GetLastWin32Error());
		}
	}
}