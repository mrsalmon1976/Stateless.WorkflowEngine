/*
 * This work (Modern Encryption of a String C#, by James Tuley),
 * identified by James Tuley, is free of known copyright restrictions.
 * https://gist.github.com/4336842
 * http://creativecommons.org/publicdomain/mark/1.0/
 */

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System.Text;

namespace Stateless.WorkflowEngine.WebConsole.Security
{
    /// <summary>
    /// AES-GCM encryption used by web console versions prior to 6 to encrypt connection passwords.  This is
    /// only retained so that passwords stored by older versions can be migrated to data protection - it uses a
    /// 128-bit nonce which System.Security.Cryptography.AesGcm does not support.
    /// </summary>
    public interface ILegacyEncryptionProvider
    {
        byte[] NewKey();

        string SimpleEncrypt(string secretMessage, byte[] key);

        string? SimpleDecrypt(string encryptedMessage, byte[] key);
    }

    public class LegacyEncryptionProvider : ILegacyEncryptionProvider
    {
        private static readonly SecureRandom Random = new SecureRandom();

        public const int NonceBitSize = 128;
        public const int MacBitSize = 128;
        public const int KeyBitSize = 256;

        public byte[] NewKey()
        {
            var key = new byte[KeyBitSize / 8];
            Random.NextBytes(key);
            return key;
        }

        public string SimpleEncrypt(string secretMessage, byte[] key)
        {
            if (string.IsNullOrEmpty(secretMessage))
                throw new ArgumentException("Secret Message Required!", nameof(secretMessage));
            if (key == null || key.Length != KeyBitSize / 8)
                throw new ArgumentException($"Key needs to be {KeyBitSize} bit!", nameof(key));

            var plainText = Encoding.UTF8.GetBytes(secretMessage);
            var nonce = new byte[NonceBitSize / 8];
            Random.NextBytes(nonce, 0, nonce.Length);

            var cipher = new GcmBlockCipher(new AesEngine());
            cipher.Init(true, new AeadParameters(new KeyParameter(key), MacBitSize, nonce, Array.Empty<byte>()));

            var cipherText = new byte[cipher.GetOutputSize(plainText.Length)];
            var len = cipher.ProcessBytes(plainText, 0, plainText.Length, cipherText, 0);
            cipher.DoFinal(cipherText, len);

            return Convert.ToBase64String(nonce.Concat(cipherText).ToArray());
        }

        /// <summary>
        /// Decrypts and authenticates a message, returning null if it does not authenticate.
        /// </summary>
        public string? SimpleDecrypt(string encryptedMessage, byte[] key)
        {
            if (string.IsNullOrEmpty(encryptedMessage))
                throw new ArgumentException("Encrypted Message Required!", nameof(encryptedMessage));
            if (key == null || key.Length != KeyBitSize / 8)
                throw new ArgumentException($"Key needs to be {KeyBitSize} bit!", nameof(key));

            var message = Convert.FromBase64String(encryptedMessage);
            var nonce = message.Take(NonceBitSize / 8).ToArray();
            var cipherText = message.Skip(nonce.Length).ToArray();

            var cipher = new GcmBlockCipher(new AesEngine());
            cipher.Init(false, new AeadParameters(new KeyParameter(key), MacBitSize, nonce, Array.Empty<byte>()));
            var plainText = new byte[cipher.GetOutputSize(cipherText.Length)];

            try
            {
                var len = cipher.ProcessBytes(cipherText, 0, cipherText.Length, plainText, 0);
                cipher.DoFinal(plainText, len);
            }
            catch (InvalidCipherTextException)
            {
                return null;
            }

            return Encoding.UTF8.GetString(plainText);
        }
    }
}
