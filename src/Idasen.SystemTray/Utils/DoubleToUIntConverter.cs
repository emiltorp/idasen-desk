using Idasen.SystemTray.Interfaces;
using System;

namespace Idasen.SystemTray.Utils
{
    public class DoubleToUIntConverter
        : IDoubleToUIntConverter
    {
        public uint ConvertToUInt(double value,
                                    uint defaultValue)
        {
            return !TryConvertToUInt(value,
                                        out uint uintValue)
                       ? defaultValue
                       : uintValue;
        }

        public bool TryConvertToUInt(double value,
                                       out uint uintValue)
        {
            try
            {
                uintValue = Convert.ToUInt32(value);

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