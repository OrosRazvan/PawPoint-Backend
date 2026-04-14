//using PawPoint.DB;
//using PawPoint.DB.Entities;
//using PawPoint.Services.Interfaces;
//using PawPoint.Services.Requests;
//using PawPoint.Services.Services;
//using Microsoft.AspNetCore.Http;
//using Microsoft.Data.Sqlite;
//using Microsoft.EntityFrameworkCore;
//using Moq;
//using Task = System.Threading.Tasks.Task;

//namespace PawPoint.Tests.ServiceTests
//{
//    public class UserServiceTests
//    {
//        private static Context InMemCtx(string name) =>
//            new(new DbContextOptionsBuilder<Context>().UseInMemoryDatabase(name).Options);

//        private static async Task<(Context db, SqliteConnection conn)> SqliteCtxAsync()
//        {
//            var conn = new SqliteConnection("DataSource=:memory:");
//            await conn.OpenAsync();
//            var options = new DbContextOptionsBuilder<Context>().UseSqlite(conn).Options;
//            var db = new Context(options);
//            await db.Database.EnsureCreatedAsync();
//            return (db, conn);
//        }

//        private static IFormFile MakeFile(string fileName = "pic.png", string contentType = "image/png", int size = 128)
//        {
//            var bytes = new byte[size];
//            for (int i = 0; i < size; i++) { bytes[i] = 1; }
//            var stream = new MemoryStream(bytes);
//            return new FormFile(stream, 0, bytes.Length, "file", fileName)  
//            {
//                Headers = new HeaderDictionary(),
//                ContentType = contentType
//            };
//        }

//        #region GetProfile
//        [Fact]
//        public async Task GetProfileAsync_UserExists_ReturnsProfile()
//        {
//            const int userId = 1;
//            const string rel = "users/1/pp.webp";

//            var pics = new Mock<IProfilePictureService>(MockBehavior.Strict);
//            var urlf = new Mock<IProfilePictureUrlFactory>();
//            urlf.Setup(f => f.BuildPublicUrl(rel)).Returns("FULL:" + rel);
//            var pii = new Mock<IPiiEncryptionService>();
//            pii.Setup(p => p.Decrypt(It.IsAny<string>())).Returns((string s) => s);

//            await using var db = InMemCtx(nameof(GetProfileAsync_UserExists_ReturnsProfile));
//            var pref = new NotificationPreference { Id = 10, Name = "All" };
//            db.Add(pref);
//            db.Add(new User
//            {
//                Id = userId,
//                Email = "john@example.com",
//                EmailHash = "eh",
//                PasswordHash = "ph",
//                FullName = "John Doe",
//                PhoneNumber = "123",
//                ProfilePictureUrl = rel,
//                NotificationPreference = pref,
//                IsDeleted = false
//            });
//            await db.SaveChangesAsync();

//            var sut = new UserService(db, pics.Object, urlf.Object, pii.Object);
//            var result = await sut.GetProfileAsync(userId);

//            Assert.Equal("john@example.com", result.Email);
//            Assert.Equal("John Doe", result.FullName);
//            Assert.Equal("FULL:" + rel, result.ProfilePictureUrl);
//            Assert.Equal("123", result.PhoneNumber);
//            Assert.Equal("All", result.NotificationPreference);
//            urlf.Verify(f => f.BuildPublicUrl(rel), Times.Once);
//            pics.VerifyNoOtherCalls();
//        }
//        #endregion

//        #region UpdateProfile
//        [Fact]
//        public async Task UpdateProfile_NameAndPhoneOnly_UpdatesFields_NoPictureCalls()
//        {
//            await using var db = InMemCtx(nameof(UpdateProfile_NameAndPhoneOnly_UpdatesFields_NoPictureCalls));
//            var pref = new NotificationPreference { Id = 10, Name = "All" };
//            db.Add(pref);
//            db.Add(new User
//            {
//                Id = 1,
//                Email = "u1@example.com",
//                EmailHash = "h1",
//                PasswordHash = "p",
//                FullName = "User 1",
//                PhoneNumber = "000",
//                ProfilePictureUrl = "users/1/old.webp",
//                NotificationPreference = pref
//            });
//            db.SaveChanges();

//            var pics = new Mock<IProfilePictureService>(MockBehavior.Strict);
//            var urlf = new Mock<IProfilePictureUrlFactory>();
//            urlf.Setup(f => f.BuildPublicUrl("users/1/old.webp")).Returns("URL:users/1/old.webp");
//            var pii = new Mock<IPiiEncryptionService>();
//            pii.Setup(p => p.Decrypt(It.IsAny<string>())).Returns((string s) => s);

//            var sut = new UserService(db, pics.Object, urlf.Object, pii.Object);

//            var req = new UpdateUserProfileRequest("Jane Doe", "+40712345678", null);
//            var result = await sut.UpdateProfileAsync(1, req);

//            Assert.Equal("Jane Doe", result.FullName);
//            Assert.Equal("+40712345678", result.PhoneNumber);
//            Assert.Equal("URL:users/1/old.webp", result.ProfilePictureUrl);
//            pics.VerifyNoOtherCalls();
//            urlf.Verify(f => f.BuildPublicUrl("users/1/old.webp"), Times.Once);
//        }

//        [Fact]
//        public async Task UpdateProfile_UploadNew_NoExisting_CallsUploadOnly()
//        {
//            await using var db = InMemCtx(nameof(UpdateProfile_UploadNew_NoExisting_CallsUploadOnly));
//            var pref = new NotificationPreference { Id = 10, Name = "All" };
//            db.Add(pref);
//            db.Add(new User
//            {
//                Id = 2,
//                Email = "u2@example.com",
//                EmailHash = "h2",
//                PasswordHash = "p",
//                FullName = "User 2",
//                NotificationPreference = pref
//            });
//            db.SaveChanges();

//            var pics = new Mock<IProfilePictureService>();
//            pics.Setup(p => p.UploadAsync(2, It.IsAny<IFormFile>())).ReturnsAsync("users/2/new.webp");
//            var urlf = new Mock<IProfilePictureUrlFactory>();
//            urlf.Setup(f => f.BuildPublicUrl("users/2/new.webp")).Returns("URL:users/2/new.webp");
//            var pii = new Mock<IPiiEncryptionService>();
//            pii.Setup(p => p.Decrypt(It.IsAny<string>())).Returns((string s) => s);

//            var sut = new UserService(db, pics.Object, urlf.Object, pii.Object);
//            var req = new UpdateUserProfileRequest("New Name", "123", MakeFile());
//            var result = await sut.UpdateProfileAsync(2, req);

//            Assert.Equal("New Name", result.FullName);
//            Assert.Equal("URL:users/2/new.webp", result.ProfilePictureUrl);
//            pics.Verify(p => p.UploadAsync(2, It.IsAny<IFormFile>()), Times.Once);
//            pics.Verify(p => p.DeleteAsync(It.IsAny<string>()), Times.Never);
//            urlf.Verify(f => f.BuildPublicUrl("users/2/new.webp"), Times.Once);
//        }

//        [Fact]
//        public async Task UpdateProfile_ReplaceExisting_DeletesOld_ThenUploadsNew()
//        {
//            await using var db = InMemCtx(nameof(UpdateProfile_ReplaceExisting_DeletesOld_ThenUploadsNew));
//            var pref = new NotificationPreference { Id = 10, Name = "All" };
//            db.Add(pref);
//            db.Add(new User
//            {
//                Id = 3,
//                Email = "u3@example.com",
//                EmailHash = "h3",
//                PasswordHash = "p",
//                FullName = "User 3",
//                ProfilePictureUrl = "users/3/old.webp",
//                NotificationPreference = pref
//            });
//            db.SaveChanges();

//            var pics = new Mock<IProfilePictureService>();
//            pics.Setup(p => p.DeleteAsync("users/3/old.webp")).Returns(Task.CompletedTask);
//            pics.Setup(p => p.UploadAsync(3, It.IsAny<IFormFile>())).ReturnsAsync("users/3/new.webp");
//            var urlf = new Mock<IProfilePictureUrlFactory>();
//            urlf.Setup(f => f.BuildPublicUrl("users/3/new.webp")).Returns("URL:users/3/new.webp");
//            var pii = new Mock<IPiiEncryptionService>();
//            pii.Setup(p => p.Decrypt(It.IsAny<string>())).Returns((string s) => s);

//            var sut = new UserService(db, pics.Object, urlf.Object, pii.Object);
//            var req = new UpdateUserProfileRequest("User 3", "999", MakeFile());
//            var result = await sut.UpdateProfileAsync(3, req);

//            Assert.Equal("URL:users/3/new.webp", result.ProfilePictureUrl);
//            pics.Verify(p => p.DeleteAsync("users/3/old.webp"), Times.Once);
//            pics.Verify(p => p.UploadAsync(3, It.IsAny<IFormFile>()), Times.Once);
//            urlf.Verify(f => f.BuildPublicUrl("users/3/new.webp"), Times.Once);
//        }

//        [Fact]
//        public async Task UpdateProfile_InvalidUserId_Throws()
//        {
//            await using var db = InMemCtx(nameof(UpdateProfile_InvalidUserId_Throws));
//            var pics = new Mock<IProfilePictureService>(MockBehavior.Strict);
//            var urlf = new Mock<IProfilePictureUrlFactory>(MockBehavior.Strict);
//            var pii = new Mock<IPiiEncryptionService>();
//            pii.Setup(p => p.Decrypt(It.IsAny<string>())).Returns((string s) => s);

//            var sut = new UserService(db, pics.Object, urlf.Object, pii.Object);

//            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
//                sut.UpdateProfileAsync(0, new UpdateUserProfileRequest("A", null, null)));
//        }

//        [Fact]
//        public async Task UpdateProfile_UserNotFound_Throws()
//        {
//            await using var db = InMemCtx(nameof(UpdateProfile_UserNotFound_Throws));
//            var pics = new Mock<IProfilePictureService>(MockBehavior.Strict);
//            var urlf = new Mock<IProfilePictureUrlFactory>(MockBehavior.Strict);
//            var pii = new Mock<IPiiEncryptionService>();
//            pii.Setup(p => p.Decrypt(It.IsAny<string>())).Returns((string s) => s);

//            var sut = new UserService(db, pics.Object, urlf.Object, pii.Object);

//            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
//                sut.UpdateProfileAsync(999, new UpdateUserProfileRequest("A", null, null)));
//        }
//        #endregion

//        #region ChangePassword
//        [Fact]
//        public async Task ChangePassword_WithCorrectCurrent_UpdatesHash()
//        {
//            await using var db = InMemCtx(nameof(ChangePassword_WithCorrectCurrent_UpdatesHash));
//            var pref = new NotificationPreference { Id = 10, Name = "All" };
//            db.Add(pref);
//            db.Add(new User
//            {
//                Id = 1,
//                Email = "u1@example.com",
//                EmailHash = "h1",
//                PasswordHash = "oldhash",
//                FullName = "User 1",
//                NotificationPreference = pref,
//                IsDeleted = false
//            });
//            await db.SaveChangesAsync();

//            var pics = new Mock<IProfilePictureService>(MockBehavior.Strict);
//            var urlf = new Mock<IProfilePictureUrlFactory>(MockBehavior.Strict);
//            var pii = new Mock<IPiiEncryptionService>(MockBehavior.Strict);
//            var passwords = new Mock<IPasswordService>();
//            passwords.Setup(p => p.Verify("Old!1", "oldhash")).Returns(true);
//            passwords.Setup(p => p.Hash("New!2")).Returns("newhash");

//            var sut = new UserService(db, pics.Object, urlf.Object, pii.Object, passwords.Object);
//            await sut.ChangePasswordAsync(1, new ChangePasswordRequest("Old!1", "New!2"));

//            var reloaded = await db.Users.FirstAsync(u => u.Id == 1);
//            Assert.Equal("newhash", reloaded.PasswordHash);
//        }
//        #endregion

//        #region GetSettings
//        [Fact]
//        public async Task GetSettings_ReturnsPreferenceIdAndName()
//        {
//            await using var db = InMemCtx(nameof(GetSettings_ReturnsPreferenceIdAndName));
//            var pref = new NotificationPreference { Id = 2, Name = "InvitesOnly" };
//            db.Add(pref);
//            db.Add(new User
//            {
//                Id = 7,
//                Email = "u7@example.com",
//                EmailHash = "h7",
//                PasswordHash = "pwd",
//                FullName = "User 7",
//                NotificationPreferenceId = 2,
//                NotificationPreference = pref,
//                IsDeleted = false
//            });
//            await db.SaveChangesAsync();

//            var pics = new Mock<IProfilePictureService>(MockBehavior.Strict);
//            var urlf = new Mock<IProfilePictureUrlFactory>(MockBehavior.Strict);
//            var pii = new Mock<IPiiEncryptionService>(MockBehavior.Strict);

//            var sut = new UserService(db, pics.Object, urlf.Object, pii.Object);
//            var result = await sut.GetSettingsAsync(7);

//            Assert.Equal(2, result.NotificationPreferenceId);
//            Assert.Equal("InvitesOnly", result.NotificationPreference);
//        }
//        #endregion

//        #region UpdateSettings
//        [Fact]
//        public async Task UpdateSettings_SetsPreferenceById_AndReturnsUpdated()
//        {
//            await using var db = InMemCtx(nameof(UpdateSettings_SetsPreferenceById_AndReturnsUpdated));
//            var prefOld = new NotificationPreference { Id = 1, Name = "All" };
//            var prefNew = new NotificationPreference { Id = 2, Name = "InvitesOnly" };
//            db.AddRange(prefOld, prefNew);
//            db.Add(new User
//            {
//                Id = 10,
//                Email = "u10@example.com",
//                EmailHash = "h10",
//                PasswordHash = "pwd",
//                FullName = "User 10",
//                NotificationPreferenceId = 1,
//                NotificationPreference = prefOld,
//                IsDeleted = false
//            });
//            await db.SaveChangesAsync();

//            var pics = new Mock<IProfilePictureService>(MockBehavior.Strict);
//            var urlf = new Mock<IProfilePictureUrlFactory>(MockBehavior.Strict);
//            var pii = new Mock<IPiiEncryptionService>(MockBehavior.Strict);

//            var sut = new UserService(db, pics.Object, urlf.Object, pii.Object);
//            var result = await sut.UpdateSettingsAsync(10, new UpdateUserSettingsRequest(2));

//            Assert.Equal(2, result.NotificationPreferenceId);
//            Assert.Equal("InvitesOnly", result.NotificationPreference);

//            var reloaded = await db.Users.Include(u => u.NotificationPreference).FirstAsync(u => u.Id == 10);
//            Assert.Equal(2, reloaded.NotificationPreferenceId);
//            Assert.Equal("InvitesOnly", reloaded.NotificationPreference.Name);
//        }
//        #endregion

//        #region SoftDelete
//        [Fact]
//        public async Task SoftDelete_SetsIsDeleted_AndHidesViaFilter()
//        {
//            var (db, conn) = await SqliteCtxAsync();
//            await using (conn)
//            await using (db)
//            {
//                var pref = new NotificationPreference { Id = 1, Name = "All" };
//                db.Add(pref);
//                db.Add(new User
//                {
//                    Id = 7,
//                    Email = "u7@example.com",
//                    EmailHash = "h7",
//                    PasswordHash = "p",
//                    FullName = "User 7",
//                    NotificationPreferenceId = 1,
//                    NotificationPreference = pref
//                });
//                await db.SaveChangesAsync();

//                var pics = new Mock<IProfilePictureService>(MockBehavior.Strict);
//                var urlf = new Mock<IProfilePictureUrlFactory>(MockBehavior.Strict);
//                var pii = new Mock<IPiiEncryptionService>(MockBehavior.Strict);

//                var sut = new UserService(db, pics.Object, urlf.Object, pii.Object);
//                await sut.SoftDeleteUserAsync(7);

//                db.ChangeTracker.Clear();

//                Assert.Null(await db.Users.FirstOrDefaultAsync(u => u.Id == 7));

//                var raw = await db.Users
//                    .IgnoreQueryFilters()
//                    .AsNoTracking()
//                    .FirstAsync(u => u.Id == 7);

//                Assert.True(raw.IsDeleted);
//            }
//        }
//        #endregion
//    }
//}
