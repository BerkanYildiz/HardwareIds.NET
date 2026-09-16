namespace HardwareIds.NET.Native
{
    using System;
    using System.Runtime.InteropServices;

    internal static class WinBrand
    {
        [DllImport("winbrand.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr BrandingFormatString(string pstrFormat);

        /// <summary>
        /// Expands a Windows branding format string, such as "%WINDOWS_LONG%".
        /// </summary>
        /// <param name="InFormat">The format string.</param>
        public static string? FormatString(string InFormat)
        {
            try
            {
                var Value = BrandingFormatString(InFormat);

                if (Value == IntPtr.Zero)
                    return null;

                try
                {
                    return Marshal.PtrToStringUni(Value);
                }
                finally
                {
                    Kernel32.GlobalFree(Value);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
