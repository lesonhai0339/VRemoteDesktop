using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Vsign4.VRemoteDesktop.Helpers
{
    public static class Validator
    {
        private static Regex normal = new Regex("^[a-zA-Z0-9]*$");
        public static bool ContainSpecialCharacterRegex(string input)
        {
            return !normal.IsMatch(input);
        }
        public static bool ContainSpecialCharacter(string input)
        {
            return input.Any(c => !char.IsLetterOrDigit(c));
        }
    }
}
