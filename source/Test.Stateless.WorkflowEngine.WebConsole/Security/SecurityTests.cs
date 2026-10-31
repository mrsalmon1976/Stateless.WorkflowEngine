using Microsoft.AspNetCore.DataProtection;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Security;

namespace Test.Stateless.WorkflowEngine.WebConsole.Security
{
    [TestFixture]
    public class LegacyEncryptionProviderTests
    {
        [Test]
        public void SimpleDecrypt_ReturnsOriginal_WhenEncryptedWithSameKey()
        {
            var provider = new LegacyEncryptionProvider();
            byte[] key = provider.NewKey();

            string encrypted = provider.SimpleEncrypt("my-secret", key);

            Assert.That(provider.SimpleDecrypt(encrypted, key), Is.EqualTo("my-secret"));
        }

        [Test]
        public void SimpleDecrypt_ReturnsNull_WhenKeyIsWrong()
        {
            var provider = new LegacyEncryptionProvider();
            string encrypted = provider.SimpleEncrypt("my-secret", provider.NewKey());

            Assert.That(provider.SimpleDecrypt(encrypted, provider.NewKey()), Is.Null);
        }
    }

    [TestFixture]
    public class ConnectionPasswordProtectorTests
    {
        private LegacyEncryptionProvider _legacyEncryptionProvider = null!;
        private ConnectionPasswordProtector _protector = null!;

        [SetUp]
        public void SetUp()
        {
            _legacyEncryptionProvider = new LegacyEncryptionProvider();
            _protector = new ConnectionPasswordProtector(new EphemeralDataProtectionProvider(), _legacyEncryptionProvider);
        }

        [Test]
        public void Unprotect_ReturnsPassword_WhenProtected()
        {
            var connection = new ConnectionModel { Password = _protector.Protect("pwd") };

            Assert.That(connection.Password, Is.Not.EqualTo("pwd"));
            Assert.That(_protector.Unprotect(connection), Is.EqualTo("pwd"));
        }

        [Test]
        public void Unprotect_ReturnsNull_WhenNoPassword()
        {
            Assert.That(_protector.Unprotect(new ConnectionModel()), Is.Null);
        }

        [Test]
        public void Unprotect_DecryptsLegacyPassword_WhenKeyIsSet()
        {
            var connection = CreateLegacyConnection("legacy-pwd");

            Assert.That(_protector.Unprotect(connection), Is.EqualTo("legacy-pwd"));
        }

        [Test]
        public void MigrateLegacyPassword_ReprotectsPasswordAndClearsKey()
        {
            var connection = CreateLegacyConnection("legacy-pwd");
            string legacyCipherText = connection.Password!;

            bool result = _protector.MigrateLegacyPassword(connection);

            Assert.That(result, Is.True);
            Assert.That(connection.Key, Is.Null);
            Assert.That(connection.Password, Is.Not.EqualTo(legacyCipherText));
            Assert.That(_protector.Unprotect(connection), Is.EqualTo("legacy-pwd"));
        }

        [Test]
        public void MigrateLegacyPassword_ReturnsFalse_WhenNotLegacy()
        {
            var connection = new ConnectionModel { Password = _protector.Protect("pwd") };
            string password = connection.Password;

            Assert.That(_protector.MigrateLegacyPassword(connection), Is.False);
            Assert.That(connection.Password, Is.EqualTo(password));
        }

        private ConnectionModel CreateLegacyConnection(string password)
        {
            byte[] key = _legacyEncryptionProvider.NewKey();
            return new ConnectionModel
            {
                Key = Convert.ToBase64String(key),
                Password = _legacyEncryptionProvider.SimpleEncrypt(password, key)
            };
        }
    }

    [TestFixture]
    public class PasswordProviderTests
    {
        [Test]
        public void CheckPassword_ReturnsTrue_WhenPasswordMatchesHash()
        {
            var provider = new PasswordProvider();
            string hash = provider.HashPassword("password");

            Assert.That(provider.CheckPassword("password", hash), Is.True);
            Assert.That(provider.CheckPassword("wrong", hash), Is.False);
        }

        [Test]
        public void CheckPassword_ReturnsFalse_WhenHashInvalid()
        {
            var provider = new PasswordProvider();

            Assert.That(provider.CheckPassword("password", "not-a-hash"), Is.False);
            Assert.That(provider.CheckPassword("password", null), Is.False);
        }

    }

    [TestFixture]
    public class UserPrincipalFactoryTests
    {
        [Test]
        public void GetPermissions_ReturnsAllClaims_WhenAdmin()
        {
            var user = TestHelpers.CreateUser(role: Roles.Admin);

            Assert.That(UserPrincipalFactory.GetPermissions(user), Is.EquivalentTo(Claims.AllClaims));
        }

        [Test]
        public void GetPermissions_ReturnsUserClaims_WhenNotAdmin()
        {
            var user = TestHelpers.CreateUser("user", Roles.User, Claims.UserList, Claims.SuspendWorkflow, "Bogus");

            Assert.That(UserPrincipalFactory.GetPermissions(user), Is.EquivalentTo(new[] { Claims.UserList, Claims.SuspendWorkflow }));
        }

        [Test]
        public void Create_SetsIdentityAndPermissionClaims()
        {
            var user = TestHelpers.CreateUser("joe", Roles.User, Claims.UserList);

            var principal = UserPrincipalFactory.Create(user);

            Assert.That(principal.Identity!.IsAuthenticated, Is.True);
            Assert.That(principal.Identity.Name, Is.EqualTo("joe"));
            Assert.That(UserPrincipalFactory.GetUserId(principal), Is.EqualTo(user.Id));
            Assert.That(principal.HasClaim(Claims.PermissionClaimType, Claims.UserList), Is.True);
            Assert.That(principal.HasClaim(Claims.PermissionClaimType, Claims.UserAdd), Is.False);
        }
    }
}
