using Idasen.SystemTray.Interfaces;
using System;

namespace Idasen.SystemTray.Utils
{
    public class StringToUIntConverter
        : IStringToUIntConverter
    {
        public ulong ConvertToULong(string text,
                                   ulong defaultValue)
        {
            return !TryConvertToULong(text,
                                     out ulong uintValue)
                       ? defaultValue
                       : uintValue;
        }

        public bool TryConvertToULong(string text,
                                     out ulong uintValue)
        {
            try
            {
                uintValue = Convert.ToUInt64(text);

                return true;
            }
            catch (Exception)
            {
                uintValue = 0;

                return false;
            }
        }
    }
}