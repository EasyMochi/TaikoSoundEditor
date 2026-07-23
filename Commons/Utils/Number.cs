using System;
using System.Globalization;

namespace TaikoSoundEditor.Commons.Utils
{
    internal static class Number
    {
        public static int ParseInt(string value)
        {
            try
            {
                return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
            }
            catch(FormatException ex)
            {
                throw new FormatException($"{ex.Message} : '{value ?? "<null>"}' to int", ex);
            }
        }

        public static float ParseFloat(string value)
        {
            try
            {
                return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            catch (FormatException ex)
            {
                throw new FormatException($"{ex.Message} : {value} to float", ex);
            }
        }
    }
}
