using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WPFHelpers
{
    public sealed class MessageBoxCenterer : IDisposable
    {
        private readonly IntPtr _ownerHwnd;
        private readonly IntPtr _hHook;

        // Windows hook types
        private const int WH_CBT = 5;
        private const int HCBT_ACTIVATE = 5;

        private HookProc _hookProc; // keep delegate alive

        public MessageBoxCenterer(Window owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));

            _ownerHwnd = new WindowInteropHelper(owner).Handle;
            _hookProc = HookCallback;

            // Hook only the current thread (important)
            uint threadId = GetCurrentThreadId();
            _hHook = SetWindowsHookEx(WH_CBT, _hookProc, IntPtr.Zero, threadId);
        }

        public void Dispose()
        {
            if (_hHook != IntPtr.Zero)
                UnhookWindowsHookEx(_hHook);

            _hookProc = null;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == HCBT_ACTIVATE)
            {
                // wParam = messagebox window handle
                CenterWindow(wParam, _ownerHwnd);

                // unhook ASAP (only need first activation)
                UnhookWindowsHookEx(_hHook);
            }

            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private static void CenterWindow(IntPtr childHwnd, IntPtr parentHwnd)
        {
            if (childHwnd == IntPtr.Zero || parentHwnd == IntPtr.Zero)
                return;

            RECT childRect, parentRect;
            if (!GetWindowRect(childHwnd, out childRect)) return;
            if (!GetWindowRect(parentHwnd, out parentRect)) return;

            int childW = childRect.Right - childRect.Left;
            int childH = childRect.Bottom - childRect.Top;

            int parentW = parentRect.Right - parentRect.Left;
            int parentH = parentRect.Bottom - parentRect.Top;

            int x = parentRect.Left + (parentW - childW) / 2;
            int y = parentRect.Top + (parentH - childH) / 2;

            // SWP flags: no size change, no z-order change, no activate changes
            SetWindowPos(childHwnd, IntPtr.Zero, x, y, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }

        // Win32 interop
        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
    }
}
