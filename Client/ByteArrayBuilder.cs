using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Client
{
    internal class ByteArrayBuilder
    {
        public List<byte> lsByte;
        public ByteArrayBuilder()
        {
            lsByte = new List<byte>();
        }
        #region Attributes
        public int Length
        {
            get
            {
                int result;
                try
                {
                    result = lsByte.Count;
                }
                catch (Exception ex)
                {
                    result = 0;
                }
                return result;
            }
        }

        #endregion
        #region Functions
        public void Append(byte[] byteArray, int int0, int int1)
        {
            checked
            {
                if (int1 != 0)
                {
                    int count = lsByte.Count;
                    lsByte.AddRange(byteArray);
                    if (byteArray.Length != int1)
                    {
                        lsByte.RemoveRange(count + int0 + int1, byteArray.Length - int1 - int0);
                    }
                    if (int0 != 0)
                    {
                        lsByte.RemoveRange(count, int0);
                    }
                }
            }
        }
        public void Clear()
        {
            lsByte.Clear();
        }
        public List<byte> Cut(int int0)
        {
            List<byte> range = lsByte.GetRange(0, int0);
            lsByte.RemoveRange(0, int0);
            return range;
        }
        public int Find(byte[] byteArray)
        {
            checked
            {
                int result;
                if (Check(lsByte, byteArray))
                {
                    result = -1;
                }
                else
                {
                    int num = lsByte.Count - 1;
                    for (int i = 0; i <= num; i++)
                    {
                        if (IsExisted(lsByte, i, byteArray))
                        {
                            return i;
                        }
                    }
                    result = -1;
                }
                return result;
            }
        }
        public bool IsExisted(List<byte> source, int index, byte[] data)
        {
            checked
            {
                bool result;
                if (data.Length > source.Count - 1)
                {
                    return false;
                }
                else
                {
                    int num = data.Length - 1;
                    for (int i = 0; i <= num; i++)
                    {
                        if (source[index + i] != source[i])
                        {
                            return false;
                        }
                    }
                    result = true;

                }
                return result;
            }
        }
        public bool Check(List<byte> bytes, byte[] byteArray)
        {
            return bytes == null
                || byteArray == null
                || bytes.Count == 0
                || byteArray.Length == 0
                || byteArray.Length > bytes.Count;
        }
        public void RemoveFirst(int index)
        {
            lsByte.RemoveRange(0, index);
        }
        #endregion
    }
}
