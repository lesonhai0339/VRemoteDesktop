using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Services.SessionManagement.DTOs
{
    public class ScreenDataDto
    {
        public ScreenDataDto(byte[] buffer, int offset, int length)
        {
            Buffer = buffer;
            Offset = offset;
            Length = length;
        }

        public byte[] Buffer { get; private set; }
        public int Offset { get; private set; }
        public int Length { get; private set; }
    }
}
