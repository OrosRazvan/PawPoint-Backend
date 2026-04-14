//using Hangfire;
//using Hangfire.Common;
//using Hangfire.States;
//using PawPoint.DB;
//using PawPoint.DB.Entities;
//using PawPoint.DB.Enums;
//using PawPoint.Services.Interfaces;
//using PawPoint.Services.Requests;
//using PawPoint.Services.Responses;
//using PawPoint.Services.Services;
//using Microsoft.Data.Sqlite;
//using Microsoft.EntityFrameworkCore;
//using System.Linq.Expressions;
//using SystemTask = System.Threading.Tasks.Task;

//namespace PawPoint.Tests.ServiceTests
//{
//    public class NotificationServiceTests
//    {
//        private static NotificationService MakeService(Context ctx)
//            => new NotificationService(ctx, new FakeRealtime(), new FakeJobs());

//        private static Context CreateSqliteInMemoryContext(out SqliteConnection connection)
//        {
//            connection = new SqliteConnection("Filename=:memory:");
//            connection.Open();

//            var options = new DbContextOptionsBuilder<Context>()
//                .UseSqlite(connection)
//                .Options;

//            var ctx = new Context(options);
//            ctx.Database.EnsureCreated();
//            return ctx;
//        }

//        private static (NotificationType invitation, NotificationType reminder) SeedTypes(Context ctx)
//        {
//            var invitation = new NotificationType { Name = "Invitation" };
//            var reminder = new NotificationType { Name = "Reminder" };
//            ctx.NotificationTypes.AddRange(invitation, reminder);
//            ctx.SaveChanges();
//            return (invitation, reminder);
//        }

//        private static User SeedUser(Context ctx, string email = "user@test.local")
//        {
//            var pref = ctx.NotificationPreferences.FirstOrDefault()
//                       ?? ctx.NotificationPreferences.Add(new NotificationPreference { Name = "Default" }).Entity;
//            ctx.SaveChanges();

//            var user = new User
//            {
//                Email = email,
//                EmailHash = email.ToLowerInvariant(), 
//                PasswordHash = "pwd",
//                FullName = "Test User",
//                NotificationPreferenceId = pref.Id,
//                CreatedAt = DateTime.UtcNow,
//                UpdatedAt = DateTime.UtcNow,
//                IsDeleted = false,
//                IsEmailConfirmed = true,
//                Has2FA = false
//            };
//            ctx.Users.Add(user);
//            ctx.SaveChanges();
//            return user;
//        }

//        private static void SeedNotifications(Context ctx, int userId, NotificationType invitation, NotificationType reminder)
//        {
//            var now = DateTime.UtcNow;

//            var items = new[]
//            {
//                new Notification
//                {
//                    UserId = userId,
//                    Name = "Meeting",
//                    Content = "Team sync",
//                    IsRead = false,
//                    CreatedAt = now.AddMinutes(-10),
//                    NotificationTypeId = invitation.Id,
//                    NotificationType = invitation,
//                    IsDeleted = false
//                },
//                new Notification
//                {
//                    UserId = userId,
//                    Name = "Deadline",
//                    Content = "Submit report",
//                    IsRead = true,
//                    CreatedAt = now.AddMinutes(-5),
//                    NotificationTypeId = reminder.Id,
//                    NotificationType = reminder,
//                    IsDeleted = false
//                },
//                new Notification
//                {
//                    UserId = userId,
//                    Name = "Party Invite",
//                    Content = "Friday event",
//                    IsRead = false,
//                    CreatedAt = now.AddMinutes(-2),
//                    NotificationTypeId = invitation.Id,
//                    NotificationType = invitation,
//                    IsDeleted = false
//                }
//            };

//            ctx.Notifications.AddRange(items);
//            ctx.SaveChanges();
//        }

//        #region GetAllNotifications Tests
//        private static void SeedManyNotifications(Context ctx, int userId, NotificationType invitation, NotificationType reminder, int count)
//        {
//            var now = DateTime.UtcNow;

//            var items = Enumerable.Range(0, count)
//                .Select(i => new Notification
//                {
//                    UserId = userId,
//                    Name = $"N{i}",
//                    Content = "bulk",
//                    IsRead = i % 2 == 0, 
//                    CreatedAt = now.AddMinutes(-i),
//                    NotificationTypeId = (i % 2 == 0) ? invitation.Id : reminder.Id,
//                    NotificationType = (i % 2 == 0) ? invitation : reminder,
//                    IsDeleted = false
//                })
//                .ToList();

//            ctx.Notifications.AddRange(items);
//            ctx.SaveChanges();
//        }

//        private static (int readPri, int typePri, DateTime createdDesc) SortKey(NotificationResponse n) =>
//            (n.IsRead ? 1 : 0, n.TypeName == "Invitation" ? 0 : 1, n.CreatedAt);

//        [Fact]
//        public async SystemTask NoFilters_ReturnsAllForUser()
//        {
//            using var ctx = CreateSqliteInMemoryContext(out var conn);
//            var (invitation, reminder) = SeedTypes(ctx);
//            var user = SeedUser(ctx);
//            SeedNotifications(ctx, user.Id, invitation, reminder);

//            var service = MakeService(ctx);
//            var result = await service.GetAllNotificationsAsync(user.Id, null, null);

//            Assert.Equal(3, result.Items.Count);
//            Assert.Contains(result.Items, n => n.TypeName == "Invitation");
//            Assert.Contains(result.Items, n => n.TypeName == "Reminder");

//            conn.Close();
//        }

//        [Fact]
//        public async SystemTask IsRead_Filter_Works()
//        {
//            using var ctx = CreateSqliteInMemoryContext(out var conn);
//            var (invitation, reminder) = SeedTypes(ctx);
//            var user = SeedUser(ctx);
//            SeedNotifications(ctx, user.Id, invitation, reminder);

//            var service = MakeService(ctx);

//            var read = await service.GetAllNotificationsAsync(user.Id, true, null);
//            Assert.All(read.Items, n => Assert.True(n.IsRead));

//            var unread = await service.GetAllNotificationsAsync(user.Id, false, null);
//            Assert.All(unread.Items, n => Assert.False(n.IsRead));

//            conn.Close();
//        }

//        [Fact]
//        public async SystemTask TypeId_Filter_And_Validation()
//        {
//            using var ctx = CreateSqliteInMemoryContext(out var conn);
//            var (invitation, reminder) = SeedTypes(ctx);
//            var user = SeedUser(ctx);
//            SeedNotifications(ctx, user.Id, invitation, reminder);

//            var service = MakeService(ctx);

//            var onlyInvitation = await service.GetAllNotificationsAsync(user.Id, null, invitation.Id);
//            Assert.All(onlyInvitation.Items, n => Assert.Equal(invitation.Id, n.TypeId));

//            var onlyReminder = await service.GetAllNotificationsAsync(user.Id, null, reminder.Id);
//            Assert.All(onlyReminder.Items, n => Assert.Equal(reminder.Id, n.TypeId));

//            await Assert.ThrowsAsync<ArgumentException>(() =>
//                service.GetAllNotificationsAsync(user.Id, null, 0));

//            conn.Close();
//        }

//        [Fact]
//        public async SystemTask Pagination_Works()
//        {
//            using var ctx = CreateSqliteInMemoryContext(out var conn);
//            var (invitation, reminder) = SeedTypes(ctx);
//            var user = SeedUser(ctx);
//            SeedManyNotifications(ctx, user.Id, invitation, reminder, count: 30);

//            var service = MakeService(ctx);

//            var page1 = await service.GetAllNotificationsAsync(user.Id, null, null, pageNumber: 1, pageSize: 10);
//            var page2 = await service.GetAllNotificationsAsync(user.Id, null, null, pageNumber: 2, pageSize: 10);

//            Assert.Equal(10, page1.Items.Count);
//            Assert.Equal(10, page2.Items.Count);
//            Assert.True(page1.TotalCount >= 20);
//            Assert.Equal(1, page1.PageNumber);
//            Assert.Equal(2, page2.PageNumber);

//            var ids = page1.Items.Select(x => x.Id).Concat(page2.Items.Select(x => x.Id)).ToList();
//            Assert.Equal(ids.Count, ids.Distinct().Count());

//            var lastP1 = SortKey(page1.Items.Last());
//            var firstP2 = SortKey(page2.Items.First());
//            Assert.True(
//                lastP1.readPri < firstP2.readPri
//                || (lastP1.readPri == firstP2.readPri && lastP1.typePri < firstP2.typePri)
//                || (lastP1.readPri == firstP2.readPri && lastP1.typePri == firstP2.typePri && lastP1.createdDesc >= firstP2.createdDesc)
//            );

//            conn.Close();
//        }

//        [Fact]
//        public async System.Threading.Tasks.Task Ordering_Puts_Invitation_Unread_First()
//        {
//            using var ctx = CreateSqliteInMemoryContext(out var conn);
//            var (invitation, reminder) = SeedTypes(ctx);
//            var user = SeedUser(ctx);

//            var now = DateTime.UtcNow;
//            ctx.Notifications.AddRange(
//                new Notification
//                {
//                    UserId = user.Id,
//                    Name = "A1",
//                    Content = "A1 content",
//                    IsRead = false,
//                    CreatedAt = now.AddMinutes(-1),
//                    NotificationTypeId = invitation.Id,
//                    NotificationType = invitation,
//                    IsDeleted = false
//                },
//                new Notification
//                {
//                    UserId = user.Id,
//                    Name = "B1",
//                    Content = "B1 content",
//                    IsRead = false,
//                    CreatedAt = now.AddMinutes(-2),
//                    NotificationTypeId = reminder.Id,
//                    NotificationType = reminder,
//                    IsDeleted = false
//                },
//                new Notification
//                {
//                    UserId = user.Id,
//                    Name = "A2",
//                    Content = "A2 content",
//                    IsRead = true,
//                    CreatedAt = now.AddMinutes(-3),
//                    NotificationTypeId = invitation.Id,
//                    NotificationType = invitation,
//                    IsDeleted = false
//                },
//                new Notification
//                {
//                    UserId = user.Id,
//                    Name = "B2",
//                    Content = "B2 content",
//                    IsRead = true,
//                    CreatedAt = now.AddMinutes(-4),
//                    NotificationTypeId = reminder.Id,
//                    NotificationType = reminder,
//                    IsDeleted = false
//                }
//            );
//            ctx.SaveChanges();

//            var service = MakeService(ctx);
//            var result = await service.GetAllNotificationsAsync(user.Id, null, null, pageNumber: 1, pageSize: 10);

//            for (int i = 1; i < result.Items.Count; i++)
//            {
//                var prev = SortKey(result.Items[i - 1]);
//                var curr = SortKey(result.Items[i]);
//                Assert.True(
//                    prev.readPri < curr.readPri
//                    || (prev.readPri == curr.readPri && prev.typePri < curr.typePri)
//                    || (prev.readPri == curr.readPri && prev.typePri == curr.typePri && prev.createdDesc >= curr.createdDesc),
//                    $"Ordering broken at index {i}"
//                );
//            }

//            Assert.False(result.Items[0].IsRead);
//            Assert.Equal("Invitation", result.Items[0].TypeName);
//        }
//        #endregion

//        #region MarkAsRead tests
//        [Fact]
//        public async SystemTask MarkAsRead_Sets_IsRead_And_ReadAt()
//        {
//            using var ctx = CreateSqliteInMemoryContext(out var conn);
//            var (invitation, _) = SeedTypes(ctx);
//            var user = SeedUser(ctx);

//            var notif = ctx.Notifications.Add(new Notification
//            {
//                UserId = user.Id,
//                Name = "X",
//                Content = "c",
//                IsRead = false,
//                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
//                NotificationTypeId = invitation.Id,
//                NotificationType = invitation,
//                IsDeleted = false
//            }).Entity;
//            ctx.SaveChanges();

//            var service = MakeService(ctx);

//            var before = DateTime.UtcNow;
//            await service.MarkAsReadAsync(user.Id, notif.Id);
//            var after = DateTime.UtcNow;

//            var reloaded = ctx.Notifications.Single(n => n.Id == notif.Id);
//            Assert.True(reloaded.IsRead);
//            Assert.NotNull(reloaded.ReadAt);
//            Assert.InRange(reloaded.ReadAt!.Value, before.AddSeconds(-1), after.AddSeconds(1));

//            conn.Close();
//        }

//        [Fact]
//        public async SystemTask MarkAsRead_IsIdempotent()
//        {
//            using var ctx = CreateSqliteInMemoryContext(out var conn);
//            var (invitation, _) = SeedTypes(ctx);
//            var user = SeedUser(ctx);

//            var notif = ctx.Notifications.Add(new Notification
//            {
//                UserId = user.Id,
//                Name = "Y",
//                Content = "c",
//                IsRead = false,
//                CreatedAt = DateTime.UtcNow.AddMinutes(-2),
//                NotificationTypeId = invitation.Id,
//                NotificationType = invitation,
//                IsDeleted = false
//            }).Entity;
//            ctx.SaveChanges();

//            var service = MakeService(ctx);

//            await service.MarkAsReadAsync(user.Id, notif.Id);
//            var first = ctx.Notifications.AsNoTracking().Single(n => n.Id == notif.Id);
//            var firstReadAt = first.ReadAt;

//            await service.MarkAsReadAsync(user.Id, notif.Id);
//            var second = ctx.Notifications.AsNoTracking().Single(n => n.Id == notif.Id);

//            Assert.True(second.IsRead);
//            Assert.Equal(firstReadAt, second.ReadAt);

//            conn.Close();
//        }

//        [Fact]
//        public async SystemTask MarkAsRead_Throws_For_OtherUser()
//        {
//            using var ctx = CreateSqliteInMemoryContext(out var conn);
//            var (invitation, _) = SeedTypes(ctx);
//            var owner = SeedUser(ctx, "owner@test.local");
//            var other = SeedUser(ctx, "other@test.local");

//            var notif = ctx.Notifications.Add(new Notification
//            {
//                UserId = owner.Id,
//                Name = "Z",
//                Content = "c",
//                IsRead = false,
//                CreatedAt = DateTime.UtcNow,
//                NotificationTypeId = invitation.Id,
//                NotificationType = invitation,
//                IsDeleted = false
//            }).Entity;
//            ctx.SaveChanges();

//            var service = MakeService(ctx);

//            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
//                service.MarkAsReadAsync(other.Id, notif.Id));

//            var reloaded = ctx.Notifications.Single(n => n.Id == notif.Id);
//            Assert.False(reloaded.IsRead);
//            Assert.Null(reloaded.ReadAt);

//            conn.Close();
//        }

//        [Fact]
//        public async SystemTask MarkAsRead_Throws_For_DeletedNotification()
//        {
//            using var ctx = CreateSqliteInMemoryContext(out var conn);
//            var (invitation, _) = SeedTypes(ctx);
//            var user = SeedUser(ctx);

//            var notif = ctx.Notifications.Add(new Notification
//            {
//                UserId = user.Id,
//                Name = "del",
//                Content = "c",
//                IsRead = false,
//                CreatedAt = DateTime.UtcNow,
//                NotificationTypeId = invitation.Id,
//                NotificationType = invitation,
//                IsDeleted = true  
//            }).Entity;
//            ctx.SaveChanges();

//            var service = MakeService(ctx);

//            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
//                service.MarkAsReadAsync(user.Id, notif.Id));

//            conn.Close();
//        }

//        #endregion

//        #region SoftDelete tests

//        [Fact]
//        public async SystemTask SoftDelete_ReadOlderThanOneDay()
//        {
//            using var ctx = CreateSqliteInMemoryContext(out var conn);
//            var (invitation, _) = SeedTypes(ctx);
//            var user = SeedUser(ctx);

//            var notif = ctx.Notifications.Add(new Notification
//            {
//                UserId = user.Id,
//                Name = "to-delete",
//                Content = "c",
//                IsRead = true,
//                ReadAt = DateTime.UtcNow.AddDays(-2),      
//                CreatedAt = DateTime.UtcNow.AddDays(-3),
//                NotificationTypeId = invitation.Id,
//                NotificationType = invitation,
//                IsDeleted = false
//            }).Entity;
//            ctx.SaveChanges();

//            var service = MakeService(ctx);
//            await service.SoftDeleteNotificationsAsync(user.Id, notif.Id);

//            ctx.ChangeTracker.Clear();

//            var reloaded = await ctx.Notifications
//                .AsNoTracking()
//                .IgnoreQueryFilters()
//                .SingleAsync(n => n.Id == notif.Id);

//            Assert.True(reloaded.IsDeleted);

//            conn.Close();
//        }

//        [Fact]
//        public async SystemTask SoftDelete_NotRead()
//        {
//            using var ctx = CreateSqliteInMemoryContext(out var conn);
//            var (invitation, _) = SeedTypes(ctx);
//            var user = SeedUser(ctx);

//            var notif = ctx.Notifications.Add(new Notification
//            {
//                UserId = user.Id,
//                Name = "not-read",
//                Content = "c",
//                IsRead = false,                           
//                ReadAt = null,
//                CreatedAt = DateTime.UtcNow.AddDays(-2),
//                NotificationTypeId = invitation.Id,
//                NotificationType = invitation,
//                IsDeleted = false
//            }).Entity;
//            ctx.SaveChanges();

//            var service = MakeService(ctx);

//            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
//                service.SoftDeleteNotificationsAsync(user.Id, notif.Id));

//            var reloaded = await ctx.Notifications.AsNoTracking().SingleAsync(n => n.Id == notif.Id);
//            Assert.False(reloaded.IsDeleted);

//            conn.Close();
//        }

//        [Fact]
//        public async SystemTask SoftDelete_ReadTooRecent()
//        {
//            using var ctx = CreateSqliteInMemoryContext(out var conn);
//            var (invitation, _) = SeedTypes(ctx);
//            var user = SeedUser(ctx);

//            var notif = ctx.Notifications.Add(new Notification
//            {
//                UserId = user.Id,
//                Name = "too-recent",
//                Content = "c",
//                IsRead = true,
//                ReadAt = DateTime.UtcNow.AddHours(-6),      
//                CreatedAt = DateTime.UtcNow.AddDays(-1),
//                NotificationTypeId = invitation.Id,
//                NotificationType = invitation,
//                IsDeleted = false
//            }).Entity;
//            ctx.SaveChanges();

//            var service = MakeService(ctx);

//            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
//                service.SoftDeleteNotificationsAsync(user.Id, notif.Id));

//            var reloaded = await ctx.Notifications.AsNoTracking().SingleAsync(n => n.Id == notif.Id);
//            Assert.False(reloaded.IsDeleted);

//            conn.Close();
//        }

//        [Fact]
//        public async SystemTask SoftDelete_OtherUserWhenEligible()
//        {
//            using var ctx = CreateSqliteInMemoryContext(out var conn);
//            var (invitation, _) = SeedTypes(ctx);
//            var owner = SeedUser(ctx, "owner@test.local");
//            var other = SeedUser(ctx, "other@test.local");

//            var notif = ctx.Notifications.Add(new Notification
//            {
//                UserId = owner.Id,
//                Name = "other-delete",
//                Content = "c",
//                IsRead = true,
//                ReadAt = DateTime.UtcNow.AddDays(-2),      
//                CreatedAt = DateTime.UtcNow.AddDays(-3),
//                NotificationTypeId = invitation.Id,
//                NotificationType = invitation,
//                IsDeleted = false
//            }).Entity;
//            ctx.SaveChanges();

//            var service = MakeService(ctx);

//            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
//                service.SoftDeleteNotificationsAsync(other.Id, notif.Id));

//            var reloaded = await ctx.Notifications.AsNoTracking().SingleAsync(n => n.Id == notif.Id);
//            Assert.False(reloaded.IsDeleted);

//            conn.Close();
//        }

//        [Fact]
//        public async SystemTask SoftDelete_AlreadyDeleted()
//        {
//            using var ctx = CreateSqliteInMemoryContext(out var conn);
//            var (invitation, _) = SeedTypes(ctx);
//            var user = SeedUser(ctx);

//            var notif = ctx.Notifications.Add(new Notification
//            {
//                UserId = user.Id,
//                Name = "deleted",
//                Content = "c",
//                IsRead = true,
//                ReadAt = DateTime.UtcNow.AddDays(-5),      
//                CreatedAt = DateTime.UtcNow.AddDays(-10),
//                NotificationTypeId = invitation.Id,
//                NotificationType = invitation,
//                IsDeleted = true
//            }).Entity;
//            ctx.SaveChanges();

//            var service = MakeService(ctx);

//            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
//                service.SoftDeleteNotificationsAsync(user.Id, notif.Id));

//            conn.Close();
//        }
//        #endregion

//        #region Test Fakes
//        sealed class FakeRealtime : INotificationRealtimeDispatcher
//            {
//                public int Calls { get; private set; }
//                public int LastUserId { get; private set; }
//                public object? LastPayload { get; private set; }

//                public SystemTask PushToUserAsync(int userId, object payload)
//                {
//                    Calls++;
//                    LastUserId = userId;
//                    LastPayload = payload;
//                    return SystemTask.CompletedTask;
//                }
//            }

//        sealed class FakeJobs : IBackgroundJobClient
//        {
//            public bool WasScheduled { get; private set; }
//            public DateTime? ScheduledAtUtc { get; private set; }
//            public LambdaExpression? Captured { get; private set; }

//            public string Create(Job job, IState state)
//            {
//                WasScheduled = true;

//                if (state != null && state.GetType().Name == "ScheduledState")
//                {
//                    var prop = state.GetType().GetProperty("EnqueueAt");
//                    if (prop != null)
//                    {
//                        var val = prop.GetValue(state);
//                        switch (val)
//                        {
//                            case DateTimeOffset dto:
//                                ScheduledAtUtc = dto.UtcDateTime;
//                                break;

//                            case DateTime dt:
//                                ScheduledAtUtc = dt.Kind == DateTimeKind.Utc
//                                    ? dt
//                                    : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
//                                break;
//                        }
//                    }
//                }

//                return Guid.NewGuid().ToString("N");
//            }

//            public string Schedule<T>(Expression<Func<T, SystemTask>> methodCall, DateTime enqueueAt)
//            {
//                WasScheduled = true;
//                ScheduledAtUtc = DateTime.SpecifyKind(enqueueAt, DateTimeKind.Utc);
//                Captured = methodCall;
//                return Guid.NewGuid().ToString("N");
//            }

//            public string Enqueue(Expression<Action> methodCall) => throw new NotImplementedException();
//            public string Enqueue(Expression<Func<SystemTask>> methodCall) => throw new NotImplementedException();
//            public string Enqueue<T>(Expression<Action<T>> methodCall) => throw new NotImplementedException();
//            public string Enqueue<T>(Expression<Func<T, SystemTask>> methodCall) => throw new NotImplementedException();

//            public string Schedule(Expression<Action> methodCall, TimeSpan delay) => throw new NotImplementedException();
//            public string Schedule(Expression<Func<SystemTask>> methodCall, TimeSpan delay) => throw new NotImplementedException();
//            public string Schedule<T>(Expression<Action<T>> methodCall, TimeSpan delay) => throw new NotImplementedException();

//            public string ContinueJobWith(string parentId, Expression<Action> methodCall) => throw new NotImplementedException();
//            public string ContinueJobWith(string parentId, Expression<Func<SystemTask>> methodCall) => throw new NotImplementedException();
//            public string ContinueJobWith<T>(string parentId, Expression<Action<T>> methodCall) => throw new NotImplementedException();
//            public string ContinueJobWith<T>(string parentId, Expression<Func<T, SystemTask>> methodCall) => throw new NotImplementedException();

//            public string ContinueJobWith(string parentId, Expression<Action> methodCall, JobContinuationOptions options) => throw new NotImplementedException();
//            public string ContinueJobWith(string parentId, Expression<Func<SystemTask>> methodCall, JobContinuationOptions options) => throw new NotImplementedException();
//            public string ContinueJobWith<T>(string parentId, Expression<Action<T>> methodCall, JobContinuationOptions options) => throw new NotImplementedException();
//            public string ContinueJobWith<T>(string parentId, Expression<Func<T, SystemTask>> methodCall, JobContinuationOptions options) => throw new NotImplementedException();

//            public void AddOrUpdate(string recurringJobId, Expression<Action> methodCall, string cronExpression, TimeZoneInfo? timeZone = null, string? queue = null) => throw new NotImplementedException();
//            public void AddOrUpdate(string recurringJobId, Expression<Func<SystemTask>> methodCall, string cronExpression, TimeZoneInfo? timeZone = null, string? queue = null) => throw new NotImplementedException();
//            public void AddOrUpdate<T>(string recurringJobId, Expression<Action<T>> methodCall, string cronExpression, TimeZoneInfo? timeZone = null, string? queue = null) => throw new NotImplementedException();
//            public void AddOrUpdate<T>(string recurringJobId, Expression<Func<T, SystemTask>> methodCall, string cronExpression, TimeZoneInfo? timeZone = null, string? queue = null) => throw new NotImplementedException();

//            public bool ChangeState(string jobId, IState state, string expectedState) => throw new NotImplementedException();

//            public IState GetState(string jobId) => throw new NotImplementedException();
//            public void Requeue(string jobId) => throw new NotImplementedException();
//            public bool Delete(string jobId) => throw new NotImplementedException();
//            public void SetJobParameter(string id, string name, string value) => throw new NotImplementedException();
//            public string GetJobParameter(string id, string name) => throw new NotImplementedException();
//        }
//        #endregion

//        #region DB 
//        private static Context CreateSqlite(out SqliteConnection connection)
//        {
//            connection = new SqliteConnection("Filename=:memory:");
//            connection.Open();

//            var options = new DbContextOptionsBuilder<Context>()
//                .UseSqlite(connection)
//                .Options;

//            var ctx = new Context(options);
//            ctx.Database.EnsureCreated();
//            return ctx;
//        }

//        private static (NotificationType alert, NotificationType invitation, NotificationType reminder, NotificationType commercial) SeedTypesId(Context ctx)
//        {
//            var alert = new NotificationType { Name = "Alert" };
//            var invitation = new NotificationType { Name = "Invitation" };
//            var reminder = new NotificationType { Name = "Reminder" };
//            var commercial = new NotificationType { Name = "Commercial" };
//            ctx.NotificationTypes.AddRange(alert, invitation, reminder, commercial);
//            ctx.SaveChanges();
//            return (alert, invitation, reminder, commercial);
//        }

//        private static int SeedUserId(Context ctx)
//        {
//            var pref = ctx.NotificationPreferences.FirstOrDefault()
//                       ?? ctx.NotificationPreferences.Add(new NotificationPreference { Name = "Default" }).Entity;
//            ctx.SaveChanges();

//            var user = new User
//            {
//                Email = "u@test.local",
//                EmailHash = "u@test.local",
//                PasswordHash = "x",
//                FullName = "U",
//                NotificationPreferenceId = pref.Id,
//                CreatedAt = DateTime.UtcNow,
//                UpdatedAt = DateTime.UtcNow,
//                IsDeleted = false,
//                IsEmailConfirmed = true
//            };
//            ctx.Users.Add(user);
//            ctx.SaveChanges();
//            return user.Id;
//        }
//        #endregion

//        #region CreateNotificationAsync tests
//        [Fact]
//        public async SystemTask Alert_Instant_Persists_And_Pushes()
//        {
//            using var ctx = CreateSqlite(out var conn);
//            SeedTypesId(ctx);
//            var uid = SeedUserId(ctx);

//            var realtime = new FakeRealtime();
//            var jobs = new FakeJobs();
//            var svc = new NotificationService(ctx, realtime, jobs);

//            var req = new NotificationCreateRequest(NotificationTypeEnum.Alert, uid, "AL", "content");
//            await svc.CreateNotificationAsync(uid, req, systemRun: false);

//            var saved = ctx.Notifications.Single();
//            Assert.Equal(uid, saved.UserId);
//            Assert.Equal("AL", saved.Name);
//            Assert.Equal("content", saved.Content);

//            Assert.Equal(1, realtime.Calls);
//            Assert.Equal(uid, realtime.LastUserId);

//            Assert.False(jobs.WasScheduled);

//            conn.Close();
//        }

//        [Fact]
//        public async SystemTask Reminder_When_NotSystemRun_Is_Scheduled_For_Next_7UTC()
//        {
//            using var ctx = CreateSqlite(out var conn);
//            SeedTypesId(ctx);
//            var uid = SeedUserId(ctx);

//            var realtime = new FakeRealtime();
//            var jobs = new FakeJobs();
//            var svc = new NotificationService(ctx, realtime, jobs);

//            var req = new NotificationCreateRequest(NotificationTypeEnum.Reminder, uid, "", "");
//            await svc.CreateNotificationAsync(uid, req, systemRun: false);

//            Assert.Empty(ctx.Notifications);

//            Assert.True(jobs.WasScheduled);
//            Assert.NotNull(jobs.ScheduledAtUtc);

//            var now = DateTime.UtcNow;
//            var expected = now.Date.AddHours(7);
//            if (now >= expected) expected = expected.AddDays(1);
//            Assert.InRange(jobs.ScheduledAtUtc!.Value, expected.AddSeconds(-5), expected.AddSeconds(5));

//            Assert.Equal(0, realtime.Calls);

//            conn.Close();
//        }

//        [Fact]
//        public async SystemTask Reminder_When_SystemRun_Persists_And_Pushes()
//        {
//            using var ctx = CreateSqlite(out var conn);
//            SeedTypesId(ctx);
//            var uid = SeedUserId(ctx);

//            var realtime = new FakeRealtime();
//            var jobs = new FakeJobs();
//            var svc = new NotificationService(ctx, realtime, jobs);

//            var req = new NotificationCreateRequest(NotificationTypeEnum.Reminder, uid, "", "");
//            await svc.CreateNotificationAsync(uid, req, systemRun: true);

//            var saved = ctx.Notifications.Single();
//            Assert.Equal("Reminder", saved.Name); 
//            Assert.Equal(uid, saved.UserId);

//            Assert.Equal(1, realtime.Calls);
//            Assert.Equal(uid, realtime.LastUserId);

//            Assert.False(jobs.WasScheduled);

//            conn.Close();
//        }
//        #endregion

//    }
//}
