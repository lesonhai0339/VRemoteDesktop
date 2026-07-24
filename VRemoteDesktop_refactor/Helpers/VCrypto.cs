using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Vsign4.VRemoteDesktop.DTOs;

namespace Vsign4.VRemoteDesktop.Helpers
{
    public static class VCrypto
    {
        public static string[] EncryptString(string[] plainTexts, string key = null)
        {
            if (string.IsNullOrEmpty(key))
                key = HeaderSchema.Key;

            return plainTexts.Select(planText => EncryptString(planText, key)).ToArray();
        }
        public static string EncryptString(string plainText, string key = null)
        {
            if (string.IsNullOrEmpty(key))
                key = HeaderSchema.Key;

            byte[] iv = new byte[16];
            byte[] array;
            using (Rijndael aes = Rijndael.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(key.PadRight(16, '0'));
                aes.IV = iv;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using (MemoryStream stream = new MemoryStream())
                {
                    using (CryptoStream crypto = new CryptoStream(stream, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter writer = new StreamWriter(crypto))
                        {
                            writer.Write(plainText);
                        }
                        array = stream.ToArray();
                    }
                }
            }

            return Convert.ToBase64String(array);
        }
        public static string[] DecryptString(string[] plainTexts, string key = null)
        {
            return plainTexts.Select(plainText => DecryptString(plainText, key)).ToArray();
        }
        public static string DecryptString(string cipherText, string key = null)
        {
            if (string.IsNullOrEmpty(key))
                key = HeaderSchema.Key;

            byte[] iv = new byte[16];
            byte[] buffer = Convert.FromBase64String(cipherText);

            using (Rijndael aes = Rijndael.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(key.PadRight(16, '0'));
                aes.IV = iv;

                ICryptoTransform deCryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using (MemoryStream memoryStream = new MemoryStream(buffer))
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, deCryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader reader = new StreamReader(cryptoStream))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
        }
    }
}
